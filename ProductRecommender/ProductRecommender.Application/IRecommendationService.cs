using ProductRecommender.Core.Entities;

namespace ProductRecommender.Application;

public interface IRecommendationService
{
    Task<IReadOnlyList<Guid>> GetRecommendationsForUserAsync(Guid userId, int limit, CancellationToken ct = default);
    Task TrainModelAsync(CancellationToken ct = default);
}