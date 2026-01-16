namespace ProductRecommender.Core.Entities;

public class RecommendationWeights
{
    public double MlWeight { get; set; } = 0.6;
    public double RatingWeight { get; set; } = 0.25;
    public double PopularityWeight { get; set; } = 0.15;
}