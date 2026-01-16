using ProductRecommender.Core.Entities;
using ProductRecommender.Infrastructure.Interfaces;
using ProductRecommender.Shared.Protos.GrpcRatingService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ProductRecommender.Application;

public class RecommendationServiceApp : IRecommendationService
{
    private readonly RatingService.RatingServiceClient _ratingClient;
    private readonly IProductRecommenderModel _model;
    private readonly IConfiguration _config;
    private readonly ILogger<RecommendationServiceApp> _logger;

    public RecommendationServiceApp(RatingService.RatingServiceClient ratingClient,
                                    IProductRecommenderModel model,
                                    IConfiguration config,
                                    ILogger<RecommendationServiceApp> logger)
    {
        _ratingClient = ratingClient;
        _model = model;
        _config = config;
        _logger = logger;
    }

    // Обучение — собираем отзывы по seed users (из конфигурации)
    public async Task TrainModelAsync(CancellationToken ct = default)
    {
        var seed = _config["Training:SeedUserIds"];
        if (string.IsNullOrWhiteSpace(seed))
        {
            _logger.LogWarning("No seed users specified in Training:SeedUserIds. Skipping training.");
            return;
        }

        var userIds = seed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.Trim())
            .Where(s => Guid.TryParse(s, out _))
            .Select(Guid.Parse)
            .ToList();

        if (!userIds.Any())
        {
            _logger.LogWarning("No valid GUIDs in Training:SeedUserIds. Skipping training.");
            return;
        }

        var training = new List<ProductRatingEntry>();

        foreach (var userId in userIds)
        {
            var req = new GetReviewsByUserIdRequest
            {
                UserId = userId.ToString(),
                Page = 1,
                PageSize = 1000
            };

            var resp = await _ratingClient.GetReviewsByUserIdAsync(req);
            foreach (var r in resp.Reviews)
            {
                // normalize rating to 0..1 or keep 1..5, choose one convention.
                var label = r.Rating; // here we keep 1..5
                training.Add(new ProductRatingEntry
                {
                    UserId = r.UserId,
                    ProductId = r.ProductId,
                    Label = label
                });
            }
        }

        if (!training.Any())
        {
            _logger.LogWarning("No training data collected from RatingService.");
            return;
        }

        await _model.TrainAsync(training, ct);
        _logger.LogInformation("Model training finished. Samples: {Count}", training.Count);
    }

    // Рекомендации для пользователя (простой: берем кандидатов из его собственных rated products)
    public async Task<IReadOnlyList<Guid>> GetRecommendationsForUserAsync(Guid userId, int limit, CancellationToken ct = default)
    {
        var request = new GetReviewsByUserIdRequest
        {
            UserId = userId.ToString(),
            Page = 1,
            PageSize = 1000
        };

        var response = await _ratingClient.GetReviewsByUserIdAsync(request);
        var userReviews = response.Reviews;

        var ratedProductIds = userReviews
            .Select(r => Guid.Parse(r.ProductId))
            .ToHashSet();

        // Кандидаты — для теста: те же продукты + (опционально) расширить список
        var candidateProducts = ratedProductIds.ToList();

        var scored = candidateProducts
            .Select(pid => new
            {
                ProductId = pid,
                Score = _model.PredictScore(userId, pid)
            })
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .Select(x => x.ProductId)
            .ToList();

        return scored;
    }
}
