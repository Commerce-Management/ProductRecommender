namespace ProductRecommender.Core.Entities;

public class ProductRatingEntry
{
    public float UserId { get; set; }
    public float ProductId { get; set; }
    public float Label { get; set; }   // my Rating from ProductReview
}