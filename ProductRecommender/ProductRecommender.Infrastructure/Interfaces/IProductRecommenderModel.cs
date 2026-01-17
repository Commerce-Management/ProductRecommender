using ProductRecommender.Core.Entities;

namespace ProductRecommender.Infrastructure.Interfaces;

public interface IProductRecommenderModel
{
    Task TrainAsync(IEnumerable<ProductRatingEntry> data, CancellationToken ct = default);
    float PredictScore(Guid userId, Guid productId);
    bool IsModelTrained();
}