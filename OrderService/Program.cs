using Messaging.Common.Extensions;
using Messaging.Common.Options;
using OrderService.Contracts.Messaging;
using OrderService.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// This registers our RabbitMqOptions section with the Options pattern in .NET.
// After this, we can inject IOptions<RabbitMqOptions>(or IOptionsMonitor< RabbitMqOptions >) into any service.
// This allows us to access RabbitMQ configuration (hostname, username, vhost, etc.) using DI.
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

// Directly fetch the RabbitMqOptions values from configuration (appsettings.json).
// This is useful when you need to immediately use these settings during service registration.
var mq = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>()!;

// Register a RabbitMQ connection/channel with the DI container.
// AddRabbitMq is a custom extension method (from Messaging.Common.Extensions) that:
// - Creates a persistent RabbitMQ connection
// - Creates an IModel (channel)
// - Registers them as singletons in the DI container
// This ensures all services reuse the same expensive RabbitMQ connection.
builder.Services.AddRabbitMq(mq.HostName, mq.UserName, mq.Password, mq.VirtualHost);

// Register the event publisher implementation as a singleton.
// IOrderEventPublisher is the contract (interface).
// RabbitMqOrderEventPublisher is the concrete implementation that publishes OrderPlacedEvent to RabbitMQ.
// Singleton lifetime is correct because publisher reuses the same RabbitMQ channel for all messages.
builder.Services.AddSingleton<IOrderEventPublisher, RabbitMqOrderEventPublisher>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/api/test-order", async (IOrderEventPublisher publisher) =>
{
    var orderPlacedEvent = new Messaging.Common.Events.OrderPlacedEvent
    {
        OrderId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        OrderNumber = "ORD-" + new Random().Next(1000, 9999),
        CustomerName = "Demo Student",
        CustomerEmail = "student@demo.com",
        PhoneNumber = "123456789",
        TotalAmount = 999.99M,
        Items = new List<Messaging.Common.Events.OrderItemLine>
        {
            new Messaging.Common.Events.OrderItemLine
            {
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = 999.99M
            }
        }
    };

    // Publish to RabbitMQ Topic Exchange
    await publisher.PublishOrderPlacedAsync(orderPlacedEvent, Guid.NewGuid().ToString());

    return Results.Ok(new { Message = "Order sent to RabbitMQ successfully!", EventData = orderPlacedEvent });
})
.WithName("TestOrderPlaced");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
