using Microsoft.AspNetCore.Mvc;

namespace ClinicalTrials.Api.Controllers;

[ApiController]
[Route("api/studies")]
public sealed class StudiesController(IHttpClientFactory httpClientFactory, ILogger<StudiesController> logger) : ControllerBase
{
    [HttpGet("{nctId}")]
    [Produces("application/json")]
    public async Task<IActionResult> GetStudyAsync(string nctId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nctId))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid NCT ID",
                Detail = "The nctId parameter is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var client = httpClientFactory.CreateClient("ClinicalTrialsGov");
        var upstreamPath = $"api/v2/studies/{Uri.EscapeDataString(nctId)}";

        try
        {
            using var upstreamResponse = await client.GetAsync(upstreamPath, cancellationToken);
            var body = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken);
            var contentType = upstreamResponse.Content.Headers.ContentType?.ToString() ?? "application/json";

            return new ContentResult
            {
                Content = body,
                ContentType = contentType,
                StatusCode = (int)upstreamResponse.StatusCode
            };
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to reach ClinicalTrials.gov for NCT ID {NctId}", nctId);

            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "ClinicalTrials.gov request failed",
                Detail = "The upstream ClinicalTrials.gov service could not be reached.",
                Status = StatusCodes.Status502BadGateway
            });
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "ClinicalTrials.gov timed out for NCT ID {NctId}", nctId);

            return StatusCode(StatusCodes.Status504GatewayTimeout, new ProblemDetails
            {
                Title = "ClinicalTrials.gov request timed out",
                Detail = "The upstream ClinicalTrials.gov service did not respond in time.",
                Status = StatusCodes.Status504GatewayTimeout
            });
        }
    }
}
