var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddHttpClient("ClinicalTrialsGov", client =>
{
    var baseUrl = builder.Configuration["ClinicalTrials:BaseUrl"]
        ?? throw new InvalidOperationException("Configuration 'ClinicalTrials:BaseUrl' is required.");

    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
