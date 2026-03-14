using Messaging.Common.Events;
// using NotificationService.Application.DTOs;
// using NotificationService.Application.Interfaces;
using NotificationService.Contracts.Messaging;
// using NotificationService.Domain.Enums;
using System.Text.Json;

// Providing missing classes locally for Option 1
namespace NotificationService.Application.DTOs
{
    public class CreateNotificationRequestDTO { public Guid UserId { get; set; } public int TypeId { get; set; } public NotificationService.Domain.Enums.NotificationChannelEnum Channel { get; set; } public int TemplateVersion { get; set; } public Dictionary<string, object> TemplateData { get; set; } public List<RecipientDTO> Recipients { get; set; } public NotificationService.Domain.Enums.NotificationPriorityEnum Priority { get; set; } public DateTime? ScheduledAtUtc { get; set; } public string CreatedBy { get; set; } }
    public class RecipientDTO { public int RecipientTypeId { get; set; } public string Email { get; set; } public string PhoneNumber { get; set; } }
}
namespace NotificationService.Domain.Enums { public enum NotificationChannelEnum { Email } public enum RecipientTypeEnum { To } public enum NotificationPriorityEnum { Normal } }
namespace NotificationService.Application.Interfaces { public interface INotificationService { Task CreateAsync(NotificationService.Application.DTOs.CreateNotificationRequestDTO request); } }


namespace NotificationService.Application.Messaging
{
    public class OrderPlacedHandler : IOrderPlacedHandler
    {
        private readonly NotificationService.Application.Interfaces.INotificationService _notificationService;

        // injects INotificationService via DI so we can call business logic.
        public OrderPlacedHandler(NotificationService.Application.Interfaces.INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        // HandleAsync is called whenever an OrderPlacedEvent is consumed from RabbitMQ.
        public async Task HandleAsync(OrderPlacedEvent evt)
        {
            Console.WriteLine($"\n[NotificationService] SUCCESS! Received OrderPlacedEvent for OrderID: {evt.OrderId} from RabbitMQ.");

            // Build structured Items array (Name, Quantity, Price)
            var items = evt.Items.Select(i => new
            {
                Name = i.ProductId.ToString(), // If you have product name, use that instead
                Quantity = i.Quantity,
                Price = i.UnitPrice
            }).ToList();

            // Serialize items into JSON so TemplateRenderer will recognize it as JsonElement
            var itemsJson = JsonSerializer.Serialize(items);

            // Build template data dictionary (keys must match placeholders in template)
            var templateData = new Dictionary<string, object>
            {
                { "CustomerName", evt.CustomerName },
                { "OrderNumber", evt.OrderNumber?.ToString() ?? string.Empty },
                { "Amount", evt.TotalAmount },
                { "Items", JsonDocument.Parse(itemsJson).RootElement } // passes structured JSON
            };

            var request = new NotificationService.Application.DTOs.CreateNotificationRequestDTO
            {
                UserId = evt.UserId,
                TypeId = 1, // "OrderPlaced" type
                Channel = NotificationService.Domain.Enums.NotificationChannelEnum.Email,
                TemplateVersion = 1,
                TemplateData = templateData,
                Recipients = new List<NotificationService.Application.DTOs.RecipientDTO>
                {
                    new NotificationService.Application.DTOs.RecipientDTO
                    {
                        RecipientTypeId = (int)NotificationService.Domain.Enums.RecipientTypeEnum.To,
                        Email = evt.CustomerEmail,
                        PhoneNumber = evt.PhoneNumber
                    }
                },
                Priority = NotificationService.Domain.Enums.NotificationPriorityEnum.Normal,
                ScheduledAtUtc = null,
                CreatedBy = "OrderPlacedHandler"
            };

            // Persist notification
            await _notificationService.CreateAsync(request);
            
            Console.WriteLine($"[NotificationService] Successfully processed notification for Customer: {evt.CustomerName} ({evt.CustomerEmail}).");
        }
    }
}
