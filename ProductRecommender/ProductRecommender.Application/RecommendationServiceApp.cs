using ProductRecommender.Core.Entities;
using ProductRecommender.Infrastructure.Interfaces;
using ProductRecommender.Shared.Protos.GrpcRatingService;
using ProductRecommender.Shared.Protos.GrpcProductService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ProductRecommender.Application;

public class RecommendationServiceApp : IRecommendationService
{
    private readonly RatingService.RatingServiceClient _ratingClient;
    private readonly ProductService.ProductServiceClient _productClient;
    private readonly IProductRecommenderModel _model;
    private readonly IConfiguration _config;
    private readonly ILogger<RecommendationServiceApp> _logger;
    private readonly RecommendationWeights _weights;

    public RecommendationServiceApp(
        RatingService.RatingServiceClient ratingClient,
        ProductService.ProductServiceClient productClient,
        IProductRecommenderModel model,
        IConfiguration config,
        ILogger<RecommendationServiceApp> logger)
    {
        _ratingClient = ratingClient;
        _productClient = productClient;
        _model = model;
        _config = config;
        _logger = logger;
        
        _weights = new RecommendationWeights
        {
            MlWeight = config.GetValue<double>("Recommendation:MlWeight", 0.6),
            RatingWeight = config.GetValue<double>("Recommendation:RatingWeight", 0.25),
            PopularityWeight = config.GetValue<double>("Recommendation:PopularityWeight", 0.15)
        };
    }

    /// <summary>
    /// Автоматическое обучение модели на активных пользователях
    /// </summary>
    public async Task TrainModelAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("=== STARTING AUTOMATIC MODEL TRAINING ===");

        // Параметры обучения из конфига
        var minReviews = _config.GetValue<int>("Training:MinReviews", 5);
        var maxUsers = _config.GetValue<int>("Training:MaxUsers", 500);
        var minTotalSamples = _config.GetValue<int>("Training:MinTotalSamples", 100);

        _logger.LogInformation("Training parameters: minReviews={MinReviews}, maxUsers={MaxUsers}", 
            minReviews, maxUsers);

        try
        {
            // 1. Получаем активных пользователей из RatingService
            var activeUsersRequest = new GetActiveUsersForTrainingRequest
            {
                MinReviews = minReviews,
                MaxUsers = maxUsers
            };

            var activeUsersResponse = await _ratingClient.GetActiveUsersForTrainingAsync(
                activeUsersRequest, 
                cancellationToken: ct);

            if (!activeUsersResponse.UserIds.Any())
            {
                _logger.LogWarning("❌ No active users found for training");
                return;
            }

            _logger.LogInformation("✅ Found {Count} active users for training", 
                activeUsersResponse.UserIds.Count);

            // 2. Собираем отзывы от всех активных пользователей
            var trainingData = new List<ProductRatingEntry>();
            var successfulUsers = 0;

            foreach (var userIdString in activeUsersResponse.UserIds)
            {
                if (!Guid.TryParse(userIdString, out var userId))
                    continue;

                try
                {
                    var reviewsRequest = new GetReviewsByUserIdRequest
                    {
                        UserId = userId.ToString(),
                        Page = 1,
                        PageSize = 1000
                    };

                    var reviewsResponse = await _ratingClient.GetReviewsByUserIdAsync(
                        reviewsRequest, 
                        cancellationToken: ct);

                    if (reviewsResponse.Reviews.Any())
                    {
                        successfulUsers++;
                        
                        foreach (var review in reviewsResponse.Reviews)
                        {
                            trainingData.Add(new ProductRatingEntry
                            {
                                UserId = review.UserId,
                                ProductId = review.ProductId,
                                Label = review.Rating
                            });
                        }

                        if (successfulUsers % 10 == 0)
                        {
                            _logger.LogInformation("  Progress: {Users} users, {Samples} samples", 
                                successfulUsers, trainingData.Count);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get reviews for user {UserId}", userId);
                }
            }

            // 3. Проверяем достаточность данных
            if (trainingData.Count < minTotalSamples)
            {
                _logger.LogWarning(
                    "❌ Not enough training data: {Count} samples (minimum: {Min})", 
                    trainingData.Count, 
                    minTotalSamples);
                return;
            }

            var uniqueUsers = trainingData.Select(d => d.UserId).Distinct().Count();
            var uniqueProducts = trainingData.Select(d => d.ProductId).Distinct().Count();

            _logger.LogInformation("📊 Training data collected:");
            _logger.LogInformation("   Total samples: {Samples}", trainingData.Count);
            _logger.LogInformation("   Unique users: {Users}", uniqueUsers);
            _logger.LogInformation("   Unique products: {Products}", uniqueProducts);
            _logger.LogInformation("   Avg samples per user: {Avg:F1}", 
                (double)trainingData.Count / uniqueUsers);

            // 4. Обучаем модель
            _logger.LogInformation("🧠 Training Matrix Factorization model...");
            await _model.TrainAsync(trainingData, ct);

            _logger.LogInformation("✅ MODEL TRAINING COMPLETED SUCCESSFULLY!");
            _logger.LogInformation("===========================================");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Training failed");
            throw;
        }
    }

    public async Task<IReadOnlyList<ProductRecommendationResult>> GetRecommendationsForUserAsync(
        Guid userId, 
        int limit, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("🎯 Getting recommendations for user {UserId}, limit={Limit}", userId, limit);

        try
        {
            // Проверяем, обучена ли модель
            if (!_model.IsModelTrained())
            {
                _logger.LogWarning("⚠️ Model not trained. Triggering automatic training...");
                await TrainModelAsync(ct);
                
                if (!_model.IsModelTrained())
                {
                    _logger.LogWarning("⚠️ Training failed or insufficient data. Returning empty recommendations.");
                    return Array.Empty<ProductRecommendationResult>();
                }
            }

            // Получаем отзывы пользователя
            var reviewsRequest = new GetReviewsByUserIdRequest
            {
                UserId = userId.ToString(),
                Page = 1,
                PageSize = 1000
            };

            var reviewsResponse = await _ratingClient.GetReviewsByUserIdAsync(reviewsRequest, cancellationToken: ct);
            var userReviews = reviewsResponse.Reviews;

            _logger.LogInformation("📝 User has {Count} reviews", userReviews.Count);

            if (!userReviews.Any())
            {
                _logger.LogWarning("⚠️ User has no reviews. Cannot generate personalized recommendations.");
                return Array.Empty<ProductRecommendationResult>();
            }

            // Получаем candidate list
            var productIdsFromReviews = userReviews.Select(r => r.ProductId).ToList();

            var candidateRequest = new GetCandidateProductIdsByProdIdsFromReviewsRequest
            {
                UserId = userId.ToString()
            };
            candidateRequest.ProductIdsFromReviews.AddRange(productIdsFromReviews);

            var candidateResponse = await _productClient.GetCandidateProductIdsByProdIdsFromReviewsAsync(
                candidateRequest, 
                cancellationToken: ct);

            _logger.LogInformation("🎲 Received {Count} candidate products", candidateResponse.ProductIdsCandidate.Count);

            if (!candidateResponse.ProductIdsCandidate.Any())
            {
                _logger.LogWarning("⚠️ No candidate products found");
                return Array.Empty<ProductRecommendationResult>();
            }

            var candidateProductIds = candidateResponse.ProductIdsCandidate
                .Where(id => Guid.TryParse(id, out _))
                .Select(Guid.Parse)
                .ToList();

            // Предсказываем ML скоры
            _logger.LogInformation("🧠 Predicting ML scores for {Count} products...", candidateProductIds.Count);

            var predictions = candidateProductIds
                .Select(productId => new
                {
                    ProductId = productId,
                    MlScore = _model.PredictScore(userId, productId)
                })
                .ToList();

            // Нормализуем ML скоры (0-1)
            var maxMlScore = predictions.Max(p => p.MlScore);
            var minMlScore = predictions.Min(p => p.MlScore);
            var mlRange = maxMlScore - minMlScore;

            var normalizedPredictions = predictions.Select(p => new
            {
                p.ProductId,
                p.MlScore,
                NormalizedMlScore = mlRange > 0 
                    ? (p.MlScore - minMlScore) / mlRange 
                    : 0.5
            }).ToList();

            // Гибридный скор
            var recommendations = normalizedPredictions
                .Select(p => new ProductRecommendationResult
                {
                    ProductId = p.ProductId,
                    UserId = userId,
                    MlScore = p.NormalizedMlScore,
                    RatingScore = 0.5,
                    PopularityScore = 0.5,
                    Score = CalculateHybridScore(p.NormalizedMlScore, 0.5, 0.5)
                })
                .OrderByDescending(r => r.Score)
                .Take(limit)
                .ToList();

            _logger.LogInformation("✅ Generated {Count} recommendations", recommendations.Count);
            
            if (recommendations.Any())
            {
                _logger.LogInformation("   Top: ProductId={ProductId}, Score={Score:F4} (ML={MlScore:F4})",
                    recommendations.First().ProductId,
                    recommendations.First().Score,
                    recommendations.First().MlScore);
            }

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to generate recommendations for user {UserId}", userId);
            throw;
        }
    }

    private double CalculateHybridScore(double mlScore, double ratingScore, double popularityScore)
    {
        return (mlScore * _weights.MlWeight) +
               (ratingScore * _weights.RatingWeight) +
               (popularityScore * _weights.PopularityWeight);
    }
}