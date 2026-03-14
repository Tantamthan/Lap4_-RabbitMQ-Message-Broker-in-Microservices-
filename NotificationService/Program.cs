using Messaging.Common.Extensions;
using Messaging.Common.Options;
using NotificationService.Application.Messaging;
using NotificationService.Contracts.Interfaces;
using NotificationService.Contracts.Messaging;
using NotificationService.Infrastructure.BackgroundJobs;
using NotificationService.Infrastructure.Messaging.Consumers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Bind RabbitMQ config
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
var mq = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>()!;

// Register RabbitMQ channel (you likely already have AddRabbitMq in Messaging.Common)
builder.Services.AddRabbitMq(mq.HostName, mq.UserName, mq.Password, mq.VirtualHost);

// Added manually so it can compile the DI setup
builder.Services.AddScoped<NotificationService.Application.Interfaces.INotificationService, NotificationService.Application.Services.NotificationService>();

// Register handler
builder.Services.AddScoped<IOrderPlacedHandler, OrderPlacedHandler>();

// Register consumer
builder.Services.AddHostedService<OrderPlacedConsumer>();

// Register Application service
builder.Services.AddScoped<INotificationProcessor, NotificationService.Application.Services.NotificationService>();

// Register Background Worker
builder.Services.AddHostedService<NotificationWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
