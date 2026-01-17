using Microsoft.ML;
using Microsoft.ML.Trainers;
using Microsoft.Extensions.Logging;
using ProductRecommender.Core.Entities;
using ProductRecommender.Infrastructure.Interfaces;

namespace ProductRecommender.Infrastructure;

public class ProductRecommenderModel : IProductRecommenderModel
{
    private readonly MLContext _mlContext;
    private readonly ILogger<ProductRecommenderModel> _logger;
    private ITransformer? _model;
    private PredictionEngine<ProductRatingEntry, ProductRatingPrediction>? _predictionEngine;
    private readonly object _lock = new();
    private bool _isTrained = false;

    public ProductRecommenderModel(ILogger<ProductRecommenderModel> logger)
    {
        _mlContext = new MLContext(seed: 42);
        _logger = logger;
    }

    public Task TrainAsync(IEnumerable<ProductRatingEntry> data, CancellationToken ct = default)
    {
        var dataList = data.ToList();
        
        if (!dataList.Any())
        {
            _logger.LogWarning("No training data provided");
            return Task.CompletedTask;
        }

        _logger.LogInformation("🧠 Training with {Count} samples", dataList.Count);
        _logger.LogInformation("   Unique users: {Users}", dataList.Select(d => d.UserId).Distinct().Count());
        _logger.LogInformation("   Unique products: {Products}", dataList.Select(d => d.ProductId).Distinct().Count());

        var dataView = _mlContext.Data.LoadFromEnumerable(dataList);

        // Matrix Factorization для collaborative filtering
        var pipeline = _mlContext.Transforms.Conversion
            .MapValueToKey("userIdEncoded", nameof(ProductRatingEntry.UserId))
            .Append(_mlContext.Transforms.Conversion
                .MapValueToKey("productIdEncoded", nameof(ProductRatingEntry.ProductId)))
            .Append(_mlContext.Recommendation().Trainers.MatrixFactorization(
                new MatrixFactorizationTrainer.Options
                {
                    MatrixColumnIndexColumnName = "userIdEncoded",
                    MatrixRowIndexColumnName = "productIdEncoded",
                    LabelColumnName = nameof(ProductRatingEntry.Label),
                    NumberOfIterations = 20,
                    ApproximationRank = 32,
                    LearningRate = 0.1
                }));

        var model = pipeline.Fit(dataView);

        lock (_lock)
        {
            _model = model;
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<ProductRatingEntry, ProductRatingPrediction>(_model);
            _isTrained = true;
        }

        _logger.LogInformation("✅ Training completed successfully");
        return Task.CompletedTask;
    }

    public float PredictScore(Guid userId, Guid productId)
    {
        if (!_isTrained || _predictionEngine == null)
        {
            return 2.5f; // средний рейтинг по умолчанию
        }

        var input = new ProductRatingEntry
        {
            UserId = userId.ToString(),
            ProductId = productId.ToString(),
            Label = 0
        };

        lock (_lock)
        {
            try
            {
                var prediction = _predictionEngine.Predict(input);
                
                // Ограничиваем предсказание диапазоном 1-5
                var clampedScore = Math.Max(1f, Math.Min(5f, prediction.Score));
                
                return clampedScore;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Prediction failed for userId={UserId}, productId={ProductId}", userId, productId);
                return 2.5f;
            }
        }
    }

    public bool IsModelTrained()
    {
        lock (_lock)
        {
            return _isTrained;
        }
    }
}