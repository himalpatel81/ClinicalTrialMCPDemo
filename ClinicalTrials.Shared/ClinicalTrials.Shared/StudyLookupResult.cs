using System.Net;

namespace ClinicalTrials.Shared;

public sealed record StudyLookupResult
{
    public HttpStatusCode? StatusCode { get; init; }

    public string ContentType { get; init; } = "application/json";

    public string Body { get; init; } = string.Empty;

    public StudyLookupFailureKind FailureKind { get; init; }

    public Exception? Exception { get; init; }

    public bool HasFailure => FailureKind != StudyLookupFailureKind.None;

    public static StudyLookupResult FromUpstream(HttpStatusCode statusCode, string? contentType, string body) =>
        new()
        {
            StatusCode = statusCode,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType,
            Body = body,
            FailureKind = StudyLookupFailureKind.None
        };

    public static StudyLookupResult RequestFailed(Exception exception) =>
        new()
        {
            FailureKind = StudyLookupFailureKind.RequestFailed,
            Exception = exception
        };

    public static StudyLookupResult TimedOut(Exception exception) =>
        new()
        {
            FailureKind = StudyLookupFailureKind.TimedOut,
            Exception = exception
        };
}
