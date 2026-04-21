using System.Net;
using Microsoft.AspNetCore.Mvc;
using Recomandare_PC.DTOs;
using Recomandare_PC.Services;

namespace Recomandare_PC.Controllers.Api;

[ApiController]
[Route("api/build-assistant")]
public class BuildAssistantController(
    ILlmRecommendationService recommendationService,
    ILogger<BuildAssistantController> logger) : ControllerBase
{
    /// <summary>
    /// POST api/build-assistant/recommend
    /// Accepts partial component input (including phone JSON payloads) and returns
    /// recommendations for missing categories based on in-stock products.
    /// </summary>
    [HttpPost("recommend")]
    public async Task<IActionResult> Recommend([FromBody] BuildAssistantRequestDto request)
    {
        logger.LogDebug(
            "API build-assistant request received. DeviceId={DeviceId}, Components={ComponentCount}",
            request.DeviceId,
            request.Components?.Count ?? 0);

        try
        {
            var response = await recommendationService.RecommendAsync(request.ToRecommendationRequest());

            logger.LogDebug(
                "API build-assistant response ready. Recommendations={RecommendationCount}",
                response.Recommendations.Count);

            return Ok(Success(response));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning(ex, "BuildAssistant API hit upstream rate limit.");
            return StatusCode((int)HttpStatusCode.TooManyRequests,
                Failure("RATE_LIMIT", "Too many requests to AI provider. Please retry in 20-60 seconds."));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "BuildAssistant API upstream call failed.");
            return StatusCode((int)HttpStatusCode.ServiceUnavailable,
                Failure("UPSTREAM_UNAVAILABLE", "AI service is temporarily unavailable. Please try again shortly."));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BuildAssistant API unexpected error.");
            return StatusCode((int)HttpStatusCode.InternalServerError,
                Failure("INTERNAL_ERROR", "Unexpected server error while generating recommendations."));
        }
    }

    private object Success(RecommendationResponse response) => new
    {
        success = true,
        traceId = HttpContext.TraceIdentifier,
        data = response,
        error = (object?)null
    };

    private object Failure(string code, string message) => new
    {
        success = false,
        traceId = HttpContext.TraceIdentifier,
        data = (object?)null,
        error = new { code, message }
    };
}
