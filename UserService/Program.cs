using User.Models;
using UserService.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DBSettings>(
    builder.Configuration.GetSection("DBSettings"));


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ServiceUser>();
builder.Services.AddHostedService<KafkaConsumerService>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.UseMetricServer();
app.UseHttpMetrics();
app.MapControllers();

app.Run();
