namespace ProductRecommender.Core.Entities;

public class ProductRecommendationResult
{
    public Guid ProductId { get; set; }
    public Guid UserId { get; set;  }
    public string? Title { get; set; }        // можно тянуть из ProductService
    public double Score { get; set; }         // общий итоговый скор
    public double MlScore { get; set; }       // от ML модели
    public double RatingScore { get; set; }   // от aggregate rating (Bayesian/avg)
    public double PopularityScore { get; set; } // по заказам/корзине
}