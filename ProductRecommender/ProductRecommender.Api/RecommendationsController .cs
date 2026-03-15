using Microsoft.AspNetCore.Mvc;
using ProductRecommender.Application;

namespace ProductRecommender.Api;

[ApiController]
[Route("api/v1/[controller]")]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;
    private readonly ILogger<RecommendationsController> _logger;

    public RecommendationsController(
        IRecommendationService recommendationService,
        ILogger<RecommendationsController> logger)
    {
        _recommendationService = recommendationService;
        _logger = logger;
    }


    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetUserRecommendations(
        Guid userId, 
        [FromQuery] int limit = 10, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Request: recommendations for {UserId}, limit={Limit}", userId, limit);
        
        var recommendations = await _recommendationService.GetRecommendationsForUserAsync(userId, limit, ct);
        
        return Ok(new 
        { 
            userId, 
            limit, 
            count = recommendations.Count,
            recommendations 
        });
    }

 
    [HttpPost("train")]
    public async Task<IActionResult> Train(CancellationToken ct = default)
    {
        _logger.LogInformation("Manual training triggered");
        
        await _recommendationService.TrainModelAsync(ct);
        
        return Ok(new { message = "Training completed successfully" });
    }
}