namespace ProductRecommender.Core.Entities;

public class ProductRatingEntry
{
    public string UserId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public float Label { get; set; }  // Rating 1-5
}