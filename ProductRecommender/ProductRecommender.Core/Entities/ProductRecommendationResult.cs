namespace ProductRecommender.Core.Entities;

public class ProductRecommendationResult
{
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public double Score { get; set; }
    public double MlScore { get; set; }
    public double RatingScore { get; set; }
    public double PopularityScore { get; set; }
}