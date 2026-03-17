using Microsoft.AspNetCore.Mvc;
using Recomandare_PC.DTOs;
using Recomandare_PC.Services;

namespace Recomandare_PC.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class RecommendationController(ILlmRecommendationService recommendationService) : ControllerBase
{
    /// <summary>
    /// POST api/recommendation
    /// Receives the client's current components and returns LLM-powered recommendations
    /// for the missing categories.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Recommend([FromBody] RecommendationRequest request)
    {
        if (request.ExistingComponents is null)
            return BadRequest("ExistingComponents cannot be null.");

        var response = await recommendationService.RecommendAsync(request);
        return Ok(response);
    }
}
