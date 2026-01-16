using Microsoft.AspNetCore.Mvc;
using ProductRecommender.Application;

namespace ProductRecommender.Api;

[ApiController]
[Route("api/v1/[controller]")]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;

    public RecommendationsController(IRecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetUserRecommendations(Guid userId, [FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var items = await _recommendationService.GetRecommendationsForUserAsync(userId, limit, ct);
        return Ok(items);
    }

    // endpoint для ручного триггера обучения (для начала)
    [HttpPost("train")]
    public async Task<IActionResult> Train(CancellationToken ct = default)
    {
        await _recommendationService.TrainModelAsync(ct);
        return Ok();
    }
}
