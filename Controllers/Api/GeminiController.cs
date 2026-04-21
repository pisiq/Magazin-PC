using Microsoft.AspNetCore.Mvc;
using Recomandare_PC.Models;
using Recomandare_PC.Services;

namespace Recomandare_PC.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class GeminiController(IGeminiRecommendationService geminiService) : ControllerBase
{
    /// <summary>
    /// POST api/gemini
    /// Receives the client's current cart components via JSON over WiFi
    /// and returns Gemini AI recommendations for the missing categories.
    /// The recommended parts will only be in-stock items.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Recommend([FromBody] List<CartItem> cartItems)
    {
        if (cartItems is null)
            return BadRequest("Cart items cannot be null.");

        var response = await geminiService.RecommendAsync(cartItems);
        return Ok(response);
    }
}
