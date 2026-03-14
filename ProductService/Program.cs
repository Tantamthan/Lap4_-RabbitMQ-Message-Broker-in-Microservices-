using Messaging.Common.Extensions;
using Messaging.Common.Options;
using ProductService.Application.Messaging;
using ProductService.Contracts.Messaging;
using ProductService.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
var mq = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>()!;
builder.Services.AddRabbitMq(mq.HostName, mq.UserName, mq.Password, mq.VirtualHost);

// Added manually so it can compile the DI setup
builder.Services.AddScoped<ProductService.Application.Interfaces.IInventoryService, DummyInventoryService>();

builder.Services.AddScoped<IOrderPlacedHandler, OrderPlacedHandler>();
builder.Services.AddHostedService<OrderPlacedConsumer>();

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

// Dummy service so we can DI it as per the option 1 instruction
public class DummyInventoryService : ProductService.Application.Interfaces.IInventoryService
{
    public Task DecreaseStockBulkAsync(List<ProductService.Application.DTOs.InventoryUpdateDTO> updates) => Task.CompletedTask;
}
