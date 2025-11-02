using Microsoft.ML;
using Microsoft.ML.Trainers;

// 1) Define CSV row
public class InteractionRow
{
    public string user_id { get; set; } = default!;
    public string product_id { get; set; } = default!;
    public float score { get; set; }
}


class Program
{
    static async Task Main(string[] args)
    {
        var ml = new MLContext(seed: 42);

// 2) Load training CSV (built from Orders + Carts)
        var data = ml.Data.LoadFromTextFile<InteractionRow>(
            path: "../../data/train_implicit.csv",
            hasHeader: true,
            separatorChar: ',');

// 3) Map IDs to keys
        var prep = ml.Transforms.Conversion.MapValueToKey("userKey", "user_id")
            .Append(ml.Transforms.Conversion.MapValueToKey("productKey", "product_id"));

// 4) Train implicit MF
        var opts = new MatrixFactorizationTrainer.Options
        {
            MatrixColumnIndexColumnName = "userKey",
            MatrixRowIndexColumnName = "productKey",
            LabelColumnName = "score",
            LossFunction = MatrixFactorizationTrainer.LossFunctionType.SquareLossOneClass,
            Alpha = 1.0f,
            Lambda = 0.1f,
            NumberOfIterations = 50,
            ApproximationRank = 64
        };

        var pipeline = prep.Append(ml.Recommendation().Trainers.MatrixFactorization(opts));

// 5) Fit and save model
        var model = pipeline.Fit(data);
        ml.Model.Save(model, data.Schema, "../../models/recommender_mf.zip");

        Console.WriteLine("Model saved to /models/recommender_mf.zip");
    }
}