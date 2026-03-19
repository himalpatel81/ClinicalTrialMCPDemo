using ClinicalTrials.Shared;
using Microsoft.AspNetCore.Mvc;

namespace ClinicalTrials.Api.Controllers;

[ApiController]
[Route("api/studies")]
public sealed class StudiesController(IStudyLookupService studyLookupService, ILogger<StudiesController> logger) : ControllerBase
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

        var result = await studyLookupService.GetStudyAsync(nctId, cancellationToken);

        if (result.FailureKind == StudyLookupFailureKind.RequestFailed)
        {
            logger.LogError(result.Exception, "Failed to reach ClinicalTrials.gov for NCT ID {NctId}", nctId);

            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "ClinicalTrials.gov request failed",
                Detail = "The upstream ClinicalTrials.gov service could not be reached.",
                Status = StatusCodes.Status502BadGateway
            });
        }

        if (result.FailureKind == StudyLookupFailureKind.TimedOut)
        {
            logger.LogWarning(result.Exception, "ClinicalTrials.gov timed out for NCT ID {NctId}", nctId);

            return StatusCode(StatusCodes.Status504GatewayTimeout, new ProblemDetails
            {
                Title = "ClinicalTrials.gov request timed out",
                Detail = "The upstream ClinicalTrials.gov service did not respond in time.",
                Status = StatusCodes.Status504GatewayTimeout
            });
        }

        return new ContentResult
        {
            Content = result.Body,
            ContentType = result.ContentType,
            StatusCode = (int?)result.StatusCode
        };
    }
}
