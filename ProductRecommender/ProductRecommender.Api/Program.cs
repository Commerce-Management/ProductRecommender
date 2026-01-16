using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProductRecommender.Application;
using ProductRecommender.Infrastructure;
using ProductRecommender.Infrastructure.Interfaces;
using ProductRecommender.Shared.Protos.GrpcOrderService;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// builder.Services.AddGrpcClient<OrderService.OrderServiceClient>(options =>
// {
//     options.Address = new Uri(builder.Configuration["gRPC:OrderService"]); 
// });
// builder.Services.AddGrpcClient<OrderService.OrderServiceClient>(options =>
// {
//     options.Address = new Uri(builder.Configuration["gRPC:CartService"]); 
// });
// builder.Services.AddGrpcClient<OrderService.OrderServiceClient>(options =>
// {
//     options.Address = new Uri(builder.Configuration["gRPC:ProductService"]); 
// });
builder.Services.AddGrpcClient<OrderService.OrderServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["gRPC:RatingService"]); 
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
    options.AddDefaultPolicy(builder =>
    {
        builder.WithOrigins("http://localhost:3001", "http://localhost:3000", "http://localhost:5040", "http://localhost")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()
            .WithExposedHeaders("X-Total-Count", "X-Page-Number", "X-Page-Size");
    });
});

builder.Services.AddGrpc();
builder.Services.AddControllers();

// for AutoMapper
// builder.Services.AddAutoMapper(cfg => { }, typeof(OrderService.Core.Profiles.OrderProfile));

// for repos and services 
// builder.Services.AddScoped<IOrderService, OrderService.Application.Services.OrderService>();

// for DB 
// builder.Services.AddDbContext<OrderDbContext>(options =>
//     options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
//         b => b.MigrationsAssembly("OrderService.Infrastructure"))
// );


builder.Services.AddSingleton<IProductRecommenderModel, ProductRecommenderModel>();
builder.Services.AddScoped<IRecommendationService, RecommendationServiceApp>();


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCookiePolicy();
app.UseCors();

// app.UseAuthentication();
// app.UseAuthorization();


app.UseHttpsRedirection();
app.MapControllers();


app.Run();