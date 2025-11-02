// /src/Exporter.Console/Program.cs
using System.Globalization;
using CsvHelper;                     // CsvHelper NuGet
using CsvHelper.Configuration;
using System.Collections.Concurrent;

class InteractionRow
{
    public string user_id { get; set; } = default!;
    public string product_id { get; set; } = default!;
    public double score { get; set; }
}

class Program
{
    // tune weights if you like
    const double ORDER_WEIGHT = 2.0;
    const double CART_WEIGHT  = 1.0;

    static async Task Main(string[] args)
    {
        // 1) fetch data via gRPC from OrderService & CartService
        var orderLines = await GetOrderLinesAsync(); // returns IEnumerable<(string userId, string productId, int qty)>
        var cartLines  = await GetCartLinesAsync();  // returns IEnumerable<(string userId, string productId, int qty)>

        // 2) aggregate to (user, product) -> score
        var agg = new ConcurrentDictionary<(string user, string product), double>();

        foreach (var ol in orderLines)
        {
            var key = (ol.userId, ol.productId);
            agg.AddOrUpdate(key, ORDER_WEIGHT * Math.Max(1, ol.qty), (_, curr) => curr + ORDER_WEIGHT * Math.Max(1, ol.qty));
        }

        foreach (var cl in cartLines)
        {
            var key = (cl.userId, cl.productId);
            agg.AddOrUpdate(key, CART_WEIGHT * Math.Max(1, cl.qty), (_, curr) => curr + CART_WEIGHT * Math.Max(1, cl.qty));
        }

        // Optional: time-window filter (e.g., last 180 days) — do it in Get* methods.

        // 3) write CSV atomically
        Directory.CreateDirectory("../../data");
        var tmpPath = "../../data/train_implicit.csv.tmp";
        var finalPath = "../../data/train_implicit.csv";

        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

        using (var writer = new StreamWriter(tmpPath))
        using (var csv = new CsvWriter(writer, cfg))
        {
            csv.WriteHeader<InteractionRow>();
            csv.NextRecord();

            foreach (var kv in agg)
            {
                var row = new InteractionRow
                {
                    user_id = kv.Key.user,
                    product_id = kv.Key.product,
                    score = Math.Round(kv.Value, 3)
                };
                csv.WriteRecord(row);
                csv.NextRecord();
            }
        }

        // replace file atomically
        if (File.Exists(finalPath)) File.Delete(finalPath);
        File.Move(tmpPath, finalPath);

        Console.WriteLine($"Wrote {finalPath} with {agg.Count} rows");
    }

    static Task<IEnumerable<(string userId, string productId, int qty)>> GetOrderLinesAsync()
    {
        // Example shape: call your OrderService via gRPC and flatten order items
        // return Task.FromResult<IEnumerable<(string,string,int)>>(
        //     orders.SelectMany(o => o.Items.Select(i => (o.UserId, i.ProductId, i.Qty)))
        // );
        return Task.FromResult<IEnumerable<(string,string,int)>>(
            new List<(string,string,int)>()
            // TODO: replace with real gRPC data
        );
    }

    static Task<IEnumerable<(string userId, string productId, int qty)>> GetCartLinesAsync()
    {
        // Example: call CartService via gRPC and flatten cart items
        return Task.FromResult<IEnumerable<(string,string,int)>>(
            new List<(string,string,int)>()
            // TODO: replace with real gRPC data
        );
    }
}
