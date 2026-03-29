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

        var response = await recommendationService.RecommendAsync(request.ToRecommendationRequest());

        logger.LogDebug(
            "API build-assistant response ready. Recommendations={RecommendationCount}",
            response.Recommendations.Count);

        return Ok(response);
    }
}

