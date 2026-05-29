using FoodSelection.Data;

using FoodSelection.Model;
using FoodSelection.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Instrumentation.Runtime;
using Prometheus;
using FoodSelection.Metrics;

using FoodSelection.Services;


var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddScoped<IFoodProductService,FoodProductService>();

builder.Services.AddSingleton<MetricService>();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "redis:6379";
    options.InstanceName = "food-redis";
});


builder.Services.AddSingleton<KafkaProducer>();
builder.Services.AddHostedService<KafkaConsumer>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resourse=>resourse.
        AddService(serviceName: builder.Environment.ApplicationName))
    .WithMetrics(metrics =>
    {
        metrics.AddPrometheusExporter();
        metrics.AddMeter("FoodSelection.API");
        metrics.AddRuntimeInstrumentation();
        metrics.AddHttpClientInstrumentation();
        metrics.AddView("http.server.request.duration",
            new ExplicitBucketHistogramConfiguration
            {
                Boundaries = new double[] { 0, 0.005, 0.01, 0.025, 0.05,
                    0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 }
            });
    });

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseHttpMetrics();
app.UseMetricServer();

app.UseHttpsRedirection();
app.UseAuthorization();


app.MapControllers();

app.Run();

public partial class Program { }
