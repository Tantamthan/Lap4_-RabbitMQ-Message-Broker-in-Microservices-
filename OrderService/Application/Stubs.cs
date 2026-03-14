using System;
using System.Collections.Generic;

namespace OrderService.Application.Services
{
    // Dummy Interfaces
    public interface IOrderService {}
    public interface IOrderRepository { 
        Task<Order> GetByIdAsync(Guid id);
        Task<Order> AddAsync(Order order);
        Task<bool> ChangeOrderStatusAsync(Guid orderId, OrderStatusEnum status, string changedBy, string reason);
    }
    public interface IUserServiceClient { 
        Task<UserDTO> GetUserByIdAsync(Guid userId, string token);
        Task<Guid> SaveOrUpdateAddressAsync(AddressDTO address, string token);
    }
    public interface IProductServiceClient { 
        Task<List<ProductStockVerificationResponseDTO>> CheckProductsAvailabilityAsync(List<ProductStockVerificationRequestDTO> requests, string token);
        Task<List<ProductDTO>> GetProductsByIdsAsync(List<Guid> productIds, string token);
    }
    public interface IPaymentServiceClient { 
        Task<PaymentResponseDTO> InitiatePaymentAsync(CreatePaymentRequestDTO request, string token);
        Task<PaymentInfoResponseDTO> GetPaymentInfoAsync(PaymentInfoRequestDTO request, string token);
    }
    public interface INotificationServiceClient {}
    public interface IMapper { 
        T Map<T>(object source);
    }
    public interface IMasterDataRepository { 
        Task<PolicyDTO> GetActiveCancellationPolicyAsync();
        Task<PolicyDTO> GetActiveReturnPolicyAsync();
    }

    // Dummy Enums
    public enum PaymentMethodEnum { COD, CreditCard }
    public enum OrderStatusEnum { Pending, Confirmed }
    public enum PaymentStatusEnum { Completed, Failed }

    // Dummy Entities/DTOs
    public class Order { 
        public Guid Id { get; set; } 
        public string OrderNumber { get; set; }
        public Guid UserId { get; set; }
        public Guid ShippingAddressId { get; set; }
        public Guid BillingAddressId { get; set; }
        public string PaymentMethod { get; set; }
        public int OrderStatusId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime OrderDate { get; set; }
        public int? CancellationPolicyId { get; set; }
        public int? ReturnPolicyId { get; set; }
        public List<OrderItem> OrderItems { get; set; }
        public decimal SubTotalAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal ShippingCharges { get; set; }
    }
    public class OrderItem { 
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal PriceAtPurchase { get; set; }
        public decimal DiscountedPrice { get; set; }
        public int Quantity { get; set; }
        public int ItemStatusId { get; set; }
    }

    public class CreateOrderRequestDTO { 
        public Guid UserId { get; set; } 
        public List<OrderItemRequestDTO> Items { get; set; } 
        public Guid? ShippingAddressId { get; set; } 
        public AddressDTO ShippingAddress { get; set; } 
        public Guid? BillingAddressId { get; set; } 
        public AddressDTO BillingAddress { get; set; } 
        public PaymentMethodEnum PaymentMethod { get; set; } 
    }
    public class OrderItemRequestDTO { public Guid ProductId { get; set; } public int Quantity { get; set; } }
    public class AddressDTO { public Guid UserId { get; set; } }
    public class UserDTO { public string FullName { get; set; } public string Email { get; set; } public string PhoneNumber { get; set; } }
    public class ProductStockVerificationRequestDTO { public Guid ProductId { get; set; } public int Quantity { get; set; } }
    public class ProductStockVerificationResponseDTO { public bool IsValidProduct { get; set; } public bool IsQuantityAvailable { get; set; } }
    public class ProductDTO { public Guid Id { get; set; } public string Name { get; set; } public decimal Price { get; set; } public decimal DiscountedPrice { get; set; } }
    public class PolicyDTO { public int Id { get; set; } }
    public class CreatePaymentRequestDTO { public Guid OrderId { get; set; } public Guid UserId { get; set; } public decimal Amount { get; set; } public PaymentMethodEnum PaymentMethod { get; set; } }
    public class PaymentResponseDTO { public string PaymentUrl { get; set; } }
    public class OrderResponseDTO { public OrderStatusEnum OrderStatus { get; set; } public PaymentMethodEnum PaymentMethod { get; set; } public string PaymentUrl { get; set; } }
    public class PaymentInfoRequestDTO { public Guid OrderId { get; set; } }
    public class PaymentInfoResponseDTO { public PaymentStatusEnum PaymentStatus { get; set; } }

    public class DummyMapper : IMapper { public T Map<T>(object source) => Activator.CreateInstance<T>(); }
}
