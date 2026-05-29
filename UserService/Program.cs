using User.Controllers;
using User.Models;
using UserService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DataBase>(
    builder.Configuration.GetSection("DataBase"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ServiceUser>();
builder.Services.AddHostedService<KafkaConsumerService>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
