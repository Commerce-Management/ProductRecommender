using Microsoft.ML;
using Microsoft.ML.Trainers;
using ProductRecommender.Core.Entities;
using ProductRecommender.Infrastructure.Interfaces;

namespace ProductRecommender.Infrastructure;

public class ProductRecommenderModel : IProductRecommenderModel
{
    private readonly MLContext _mlContext;
    private ITransformer? _model;
    private PredictionEngine<ProductRatingEntry, ProductRatingPrediction>? _predictionEngine;
    private readonly object _lock = new();

    public ProductRecommenderModel()
    {
        _mlContext = new MLContext(seed: 42);
    }

    public Task TrainAsync(IEnumerable<ProductRatingEntry> data, CancellationToken ct = default)
    {
        var dataView = _mlContext.Data.LoadFromEnumerable(data);

        // Map string IDs to keys, затем MatrixFactorization
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
                    ApproximationRank = 32
                }));

        var model = pipeline.Fit(dataView);

        lock (_lock)
        {
            _model = model;
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<ProductRatingEntry, ProductRatingPrediction>(_model);
        }

        return Task.CompletedTask;
    }

    public float PredictScore(Guid userId, Guid productId)
    {
        if (_predictionEngine == null)
            return 0f;

        var input = new ProductRatingEntry
        {
            UserId = userId.ToString(),
            ProductId = productId.ToString(),
            Label = 0 // not used for prediction
        };

        lock (_lock)
        {
            try
            {
                var prediction = _predictionEngine.Predict(input);
                return prediction.Score;
            }
            catch
            {
                return 0f;
            }
        }
    }
}
