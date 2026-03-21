using System.Globalization;
using ClinicalTrials.Mcp.Administration;
using ClinicalTrials.Mcp.Data;
using Microsoft.Extensions.Options;

var commandLine = CommandLine.Parse(args);
if (!commandLine.IsValid)
{
    CommandLine.WriteUsage(commandLine.ErrorMessage);
    return 1;
}

var connectionString = commandLine.GetRequiredOption(
        "connection-string",
        "Provide --connection-string or set CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING.")
    ?? Environment.GetEnvironmentVariable("CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING");

if (string.IsNullOrWhiteSpace(connectionString))
{
    CommandLine.WriteUsage("A client registry SQL connection string is required.");
    return 1;
}

var store = new SqlClientRegistryStore(
    Options.Create(
        new ClientRegistryDatabaseSettings
        {
            ConnectionString = connectionString
        }));
var adminService = new ClientRegistryAdminService(store, TimeProvider.System);

try
{
    switch (commandLine.Command)
    {
        case "create-client":
            await adminService.CreateClientAsync(
                clientCode: commandLine.GetRequiredOption("client-code", "Missing --client-code.")!,
                displayName: commandLine.GetRequiredOption("display-name", "Missing --display-name.")!,
                permitLimitOverride: commandLine.GetOptionalInt("permit-limit"),
                cacheTtlSecondsOverride: commandLine.GetOptionalInt("cache-ttl-seconds"),
                negativeCacheTtlSecondsOverride: commandLine.GetOptionalInt("negative-cache-ttl-seconds"),
                cancellationToken: CancellationToken.None);
            Console.WriteLine("Client created successfully.");
            break;

        case "issue-key":
            await WriteIssuedKeyAsync(
                await adminService.IssueApiKeyAsync(
                    clientCode: commandLine.GetRequiredOption("client-code", "Missing --client-code.")!,
                    keyLabel: commandLine.GetRequiredOption("key-label", "Missing --key-label.")!,
                    expiresUtc: commandLine.GetOptionalDateTimeOffset("expires-utc"),
                    cancellationToken: CancellationToken.None));
            break;

        case "rotate-key":
            await WriteIssuedKeyAsync(
                await adminService.RotateApiKeyAsync(
                    clientCode: commandLine.GetRequiredOption("client-code", "Missing --client-code.")!,
                    keyLabel: commandLine.GetRequiredOption("key-label", "Missing --key-label.")!,
                    expiresUtc: commandLine.GetOptionalDateTimeOffset("expires-utc"),
                    cancellationToken: CancellationToken.None));
            break;

        case "revoke-key":
            await adminService.RevokeApiKeyAsync(
                commandLine.GetRequiredGuid("api-key-id", "Missing or invalid --api-key-id."),
                CancellationToken.None);
            Console.WriteLine("API key revoked successfully.");
            break;

        case "deactivate-client":
            await adminService.DeactivateClientAsync(
                commandLine.GetRequiredOption("client-code", "Missing --client-code.")!,
                CancellationToken.None);
            Console.WriteLine("Client deactivated successfully.");
            break;

        default:
            CommandLine.WriteUsage($"Unknown command '{commandLine.Command}'.");
            return 1;
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static Task WriteIssuedKeyAsync(IssuedClientApiKey key)
{
    Console.WriteLine("API key issued successfully.");
    Console.WriteLine($"Client code: {key.ClientCode}");
    Console.WriteLine($"API key ID: {key.ApiKeyId}");
    Console.WriteLine($"Key label: {key.KeyLabel}");
    Console.WriteLine($"Key prefix: {key.KeyPrefix}");
    Console.WriteLine($"Created UTC: {key.CreatedUtc:O}");
    if (key.ExpiresUtc.HasValue)
    {
        Console.WriteLine($"Expires UTC: {key.ExpiresUtc.Value:O}");
    }
    Console.WriteLine($"Raw API key: {key.RawApiKey}");

    return Task.CompletedTask;
}

internal sealed class CommandLine
{
    private readonly Dictionary<string, string> options;

    private CommandLine(string command, Dictionary<string, string> options, bool isValid, string? errorMessage)
    {
        Command = command;
        this.options = options;
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public string Command { get; }

    public bool IsValid { get; }

    public string? ErrorMessage { get; }

    public static CommandLine Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CommandLine(string.Empty, [], false, "A command is required.");
        }

        var command = args[0];
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal))
            {
                return new CommandLine(command, options, false, $"Unexpected argument '{args[index]}'.");
            }

            if (index + 1 >= args.Length)
            {
                return new CommandLine(command, options, false, $"Option '{args[index]}' is missing a value.");
            }

            options[args[index][2..]] = args[index + 1];
        }

        return new CommandLine(command, options, true, null);
    }

    public string? GetRequiredOption(string name, string errorMessage)
    {
        if (options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (string.Equals(name, "connection-string", StringComparison.OrdinalIgnoreCase))
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING");
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return fromEnvironment;
            }
        }

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return null;
    }

    public int? GetOptionalInt(string name)
    {
        if (!options.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    public DateTimeOffset? GetOptionalDateTimeOffset(string name)
    {
        if (!options.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    }

    public Guid GetRequiredGuid(string name, string errorMessage)
    {
        if (options.TryGetValue(name, out var value) && Guid.TryParse(value, out var parsedValue))
        {
            return parsedValue;
        }

        throw new InvalidOperationException(errorMessage);
    }

    public static void WriteUsage(string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine();
        }

        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  create-client --connection-string <value> --client-code <value> --display-name <value> [--permit-limit <int>] [--cache-ttl-seconds <int>] [--negative-cache-ttl-seconds <int>]");
        Console.Error.WriteLine("  issue-key --connection-string <value> --client-code <value> --key-label <value> [--expires-utc <timestamp>]");
        Console.Error.WriteLine("  rotate-key --connection-string <value> --client-code <value> --key-label <value> [--expires-utc <timestamp>]");
        Console.Error.WriteLine("  revoke-key --connection-string <value> --api-key-id <guid>");
        Console.Error.WriteLine("  deactivate-client --connection-string <value> --client-code <value>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("You can also provide the connection string through CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING.");
    }
}
