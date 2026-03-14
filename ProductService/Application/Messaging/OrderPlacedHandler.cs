using Messaging.Common.Events;
// using ProductService.Application.DTOs;
// using ProductService.Application.Interfaces;
using ProductService.Contracts.Messaging;

// Providing missing DTOs and Interfaces locally so that we can type it roughly as per the tutorial (Option 1).
namespace ProductService.Application.DTOs
{
    public class InventoryUpdateDTO { public Guid ProductId { get; set; } public int Quantity { get; set; } }
}
namespace ProductService.Application.Interfaces
{
    public interface IInventoryService { Task DecreaseStockBulkAsync(List<ProductService.Application.DTOs.InventoryUpdateDTO> updates); }
}

namespace ProductService.Application.Messaging
{
    public class OrderPlacedHandler : IOrderPlacedHandler
    {
        private readonly ProductService.Application.Interfaces.IInventoryService _inventory;

        // Dependency: Inventory service used to update product stock.
        // Constructor: injects IInventoryService via Dependency Injection.
        // This allows OrderPlacedHandler to call inventory logic without being tightly coupled.
        public OrderPlacedHandler(ProductService.Application.Interfaces.IInventoryService inventory)
        {
            _inventory = inventory;
        }

        // HandleAsync: This method is triggered whenever an OrderPlacedEvent is received from RabbitMQ.
        public async Task HandleAsync(OrderPlacedEvent evt)
        {
            Console.WriteLine($"\n[ProductService] SUCCESS! Received OrderPlacedEvent for OrderID: {evt.OrderId} from RabbitMQ.");

            // Map event items into DTOs expected by the InventoryService.
            // Each order item (product + quantity) becomes an InventoryUpdateDTO.
            var stockUpdates = evt.Items.Select(i => new ProductService.Application.DTOs.InventoryUpdateDTO
            {
                ProductId = i.ProductId, // Product to update
                Quantity = i.Quantity    // Quantity to reduce
            }).ToList();

            // Call the inventory service to decrease stock for all products in bulk.
            // This ensures product quantities are reduced in the database after the order is confirmed.
            await _inventory.DecreaseStockBulkAsync(stockUpdates);
            
            Console.WriteLine($"[ProductService] Successfully prepared inventory update for {stockUpdates.Count} items.");
        }
    }
}
