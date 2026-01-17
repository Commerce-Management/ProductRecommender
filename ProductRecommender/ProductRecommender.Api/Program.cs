using System.Net;
using ProductRecommender.Application;
using ProductRecommender.Infrastructure;
using ProductRecommender.Infrastructure.Interfaces;
using ProductRecommender.Shared.Protos.GrpcRatingService;
using ProductRecommender.Shared.Protos.GrpcProductService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// gRPC Clients
builder.Services.AddGrpcClient<RatingService.RatingServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["gRPC:RatingService"]!);
});
builder.Services.AddGrpcClient<ProductService.ProductServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["gRPC:ProductService"]!);
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Any, 5268, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    options.Listen(IPAddress.Any, 5007, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(b =>
    {
        b.WithOrigins("http://localhost:3001", "http://localhost:3000", "http://localhost:5040", "http://localhost")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("X-Total-Count", "X-Page-Number", "X-Page-Size");
    });
});

builder.Services.AddGrpc();
builder.Services.AddControllers();

// ML Services
builder.Services.AddSingleton<IProductRecommenderModel, ProductRecommenderModel>();
builder.Services.AddScoped<IRecommendationService, RecommendationServiceApp>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCookiePolicy();
app.UseCors();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();