using OrderService.Contracts.Messaging;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Messaging.Common.Events;

// Providing missing DTOs and Enums so that we can type it roughly as per the tutorial (Option 1).
// As requested, this file will have compilation errors because references like IOrderRepository are strictly missing.
namespace OrderService.Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IUserServiceClient _userServiceClient;
        private readonly IProductServiceClient _productServiceClient;
        private readonly IPaymentServiceClient _paymentServiceClient;
        private readonly INotificationServiceClient _notificationServiceClient;
        private readonly IMapper _mapper;
        private readonly IMasterDataRepository _masterDataRepository;
        private readonly IConfiguration _configuration;
        private readonly IOrderEventPublisher _publisher;

        public OrderService(
            IOrderRepository orderRepository,
            IUserServiceClient userServiceClient,
            IProductServiceClient productServiceClient,
            IPaymentServiceClient paymentServiceClient,
            INotificationServiceClient notificationServiceClient,
            IMasterDataRepository masterDataRepository,
            IMapper mapper,
            IConfiguration configuration,
            IOrderEventPublisher publisher)
        {
            // Initialize dependencies with null checks for safe injection
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            _userServiceClient = userServiceClient ?? throw new ArgumentNullException(nameof(userServiceClient));
            _productServiceClient = productServiceClient ?? throw new ArgumentNullException(nameof(productServiceClient));
            _paymentServiceClient = paymentServiceClient ?? throw new ArgumentNullException(nameof(paymentServiceClient));
            _notificationServiceClient = notificationServiceClient ?? throw new ArgumentNullException(nameof(notificationServiceClient));
            _masterDataRepository = masterDataRepository ?? throw new ArgumentNullException(nameof(masterDataRepository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _publisher = publisher;
        }

        public async Task<OrderResponseDTO> CreateOrderAsync(CreateOrderRequestDTO request, string accessToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.Items == null || !request.Items.Any())
                throw new ArgumentException("Order must have at least one item.");

            // Validate that the user exists via User Microservice
            var user = await _userServiceClient.GetUserByIdAsync(request.UserId, accessToken);
            if (user == null)
                throw new InvalidOperationException("User does not exist.");

            // Resolve Shipping Address ID, either provided or created newly via User Microservice
            Guid? shippingAddressId = null;
            if (request.ShippingAddressId != null)
            {
                shippingAddressId = request.ShippingAddressId;
            }
            else if (request.ShippingAddress != null)
            {
                request.ShippingAddress.UserId = request.UserId;
                shippingAddressId = await _userServiceClient.SaveOrUpdateAddressAsync(request.ShippingAddress, accessToken);
            }

            // Resolve Billing Address ID, either provided or created newly
            Guid? billingAddressId = null;
            if (request.BillingAddressId != null)
            {
                billingAddressId = request.BillingAddressId;
            }
            else if (request.BillingAddress != null)
            {
                request.BillingAddress.UserId = request.UserId;
                billingAddressId = await _userServiceClient.SaveOrUpdateAddressAsync(request.BillingAddress, accessToken);
            }

            // Validate presence of both addresses
            if (shippingAddressId == null || billingAddressId == null)
                throw new ArgumentException("Both ShippingAddressId and BillingAddressId must be provided or created.");

            // Validate product stock availability but do not reduce stock yet
            var stockCheckRequests = request.Items.Select(i => new ProductStockVerificationRequestDTO
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList();

            var stockValidation = await _productServiceClient.CheckProductsAvailabilityAsync(stockCheckRequests, accessToken);
            if (stockValidation == null || stockValidation.Any(x => !x.IsValidProduct || !x.IsQuantityAvailable))
                throw new InvalidOperationException("One or more products are invalid or out of stock.");

            // Retrieve latest product info for accurate pricing and discount
            var productIds = request.Items.Select(i => i.ProductId).ToList();
            var products = await _productServiceClient.GetProductsByIdsAsync(productIds, accessToken);

            if (products == null || products.Count != productIds.Count)
                throw new InvalidOperationException("Failed to retrieve product details for all items.");

            try
            {
                // Fetch policies (example placeholders, adjust with your actual logic)
                int? cancellationPolicyId = null;
                int? returnPolicyId = null;

                // Example: fetch cancellation policy based on user or other criteria
                var cancellationPolicy = await _masterDataRepository.GetActiveCancellationPolicyAsync();
                if (cancellationPolicy != null)
                    cancellationPolicyId = cancellationPolicy.Id;

                var returnPolicy = await _masterDataRepository.GetActiveReturnPolicyAsync();
                if (returnPolicy != null)
                    returnPolicyId = returnPolicy.Id;

                var orderId = Guid.NewGuid();
                var orderNumber = GenerateOrderNumberFromGuid(orderId);
                var now = DateTime.UtcNow;

                var initialStatus = request.PaymentMethod == PaymentMethodEnum.COD 
                    ? OrderStatusEnum.Confirmed // COD orders confirmed immediately
                    : OrderStatusEnum.Pending; // Online payment orders start as pending

                // Create order entity
                var order = new Order
                {
                    Id = orderId,
                    OrderNumber = orderNumber,
                    UserId = request.UserId,
                    ShippingAddressId = shippingAddressId.Value,
                    BillingAddressId = billingAddressId.Value,
                    PaymentMethod = request.PaymentMethod.ToString(),
                    OrderStatusId = (int)initialStatus,
                    CreatedAt = now,
                    OrderDate = now,
                    CancellationPolicyId = cancellationPolicyId,
                    ReturnPolicyId = returnPolicyId,
                    OrderItems = new List<OrderItem>()
                };

                // Add order items with fresh product data
                foreach (var item in request.Items)
                {
                    var product = products.First(p => p.Id == item.ProductId);
                    order.OrderItems.Add(new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        PriceAtPurchase = product.Price,
                        DiscountedPrice = product.DiscountedPrice,
                        Quantity = item.Quantity,
                        ItemStatusId = (int)initialStatus
                    });
                }

                // Calculate order totals: subtotal, discount, tax, shipping, and final amount
                order.SubTotalAmount = Math.Round(order.OrderItems.Sum(i => i.PriceAtPurchase * i.Quantity), 2, MidpointRounding.AwayFromZero);
                order.DiscountAmount = Math.Round(await CalculateDiscountAmountAsync(order.OrderItems), 2, MidpointRounding.AwayFromZero);
                order.TaxAmount = Math.Round(await CalculateTaxAmountAsync(order.SubTotalAmount - order.DiscountAmount), 2, MidpointRounding.AwayFromZero);
                order.ShippingCharges = Math.Round(CalculateShippingCharges(order.SubTotalAmount - order.DiscountAmount), 2, MidpointRounding.AwayFromZero);
                order.TotalAmount = Math.Round(order.SubTotalAmount - order.DiscountAmount + order.TaxAmount + order.ShippingCharges, 2, MidpointRounding.AwayFromZero);

                // Save order to repository
                var addedOrder = await _orderRepository.AddAsync(order);
                if (addedOrder == null)
                    throw new InvalidOperationException("Failed to create order.");

                // Initiate payment via Payment Service
                var paymentRequest = new CreatePaymentRequestDTO
                {
                    OrderId = order.Id,
                    UserId = order.UserId,
                    Amount = order.TotalAmount,
                    PaymentMethod = request.PaymentMethod
                };

                var paymentResponse = await _paymentServiceClient.InitiatePaymentAsync(paymentRequest, accessToken);

                if (paymentResponse == null)
                    throw new InvalidOperationException("Payment initiation failed.");

                // For COD, immediately reserve stock and send notification
                if (request.PaymentMethod == PaymentMethodEnum.COD)
                {
                    #region Event Publishing to RabbitMQ
                    // Construct the integration event (OrderPlacedEvent)
                    var orderPlacedEvent = new OrderPlacedEvent
                    {
                        OrderId = order.Id,
                        OrderNumber = order.OrderNumber,
                        UserId = order.UserId,
                        CustomerName = user.FullName,
                        CustomerEmail = user.Email,
                        PhoneNumber = user.PhoneNumber,
                        TotalAmount = order.TotalAmount,
                        Items = order.OrderItems.Select(i => new OrderItemLine
                        {
                            ProductId = i.ProductId,
                            Quantity = i.Quantity,
                            UnitPrice = i.PriceAtPurchase
                        }).ToList()
                    };

                    // Publish the event to RabbitMQ using the shared publisher.
                    // This message will be routed to:
                    // - ProductService (to decrease stock)
                    // - NotificationService (to insert notification record).
                    // correlationId is set for traceability across logs and microservices.
                    await _publisher.PublishOrderPlacedAsync(orderPlacedEvent, Guid.NewGuid().ToString());
                    #endregion

                    // Map and return order DTO with confirmed status and no payment URL
                    var orderDto = _mapper.Map<OrderResponseDTO>(order);
                    orderDto.OrderStatus = OrderStatusEnum.Confirmed;
                    orderDto.PaymentMethod = PaymentMethodEnum.COD;
                    orderDto.PaymentUrl = null;
                    return orderDto;
                }
                else
                {
                    // Map and return order DTO with pending status and payment URL
                    var orderDto = _mapper.Map<OrderResponseDTO>(order);
                    orderDto.OrderStatus = OrderStatusEnum.Pending;
                    orderDto.PaymentMethod = request.PaymentMethod;
                    orderDto.PaymentUrl = paymentResponse.PaymentUrl;
                    return orderDto;
                }
            }
            catch (Exception ex)
            {
                //Log the Exception
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public async Task<bool> ConfirmOrderAsync(Guid orderId, string accessToken)
        {
            // Retrieve order
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
                throw new KeyNotFoundException("Order not found.");

            // Only allow confirmation if order is pending
            if (order.OrderStatusId != (int)OrderStatusEnum.Pending)
                throw new InvalidOperationException("Order is not in a pending state.");

            // Retrieve payment info from Payment Service
            var paymentInfo = await _paymentServiceClient.GetPaymentInfoAsync(
                new PaymentInfoRequestDTO { OrderId = orderId }, accessToken);

            if (paymentInfo == null)
                throw new InvalidOperationException("Payment information not found for this order.");

            if (paymentInfo.PaymentStatus != PaymentStatusEnum.Completed)
                throw new InvalidOperationException("Payment is not successful.");

            var user = await _userServiceClient.GetUserByIdAsync(order.UserId, accessToken);
            if (user == null)
                throw new InvalidOperationException("User does not exist.");

            try
            {
                // Change order status to Confirmed
                bool statusChanged = await _orderRepository.ChangeOrderStatusAsync(
                    orderId, OrderStatusEnum.Confirmed, "PaymentService", "Payment successful, order confirmed.");

                if (!statusChanged)
                    throw new InvalidOperationException("Failed to update order status.");

                // Now that the order is confirmed, publish an integration event
                // Create the event payload that downstream services need
                var orderPlacedEvent = new OrderPlacedEvent
                {
                    OrderId = order.Id,
                    UserId = order.UserId,
                    CustomerName = user.FullName,
                    CustomerEmail = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    TotalAmount = order.TotalAmount,
                    Items = order.OrderItems.Select(i => new OrderItemLine
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        UnitPrice = i.PriceAtPurchase
                    }).ToList()
                };

                // Publish the event to RabbitMQ
                // - _publisher abstracts RabbitMQ communication
                // - The message is sent to exchange "ecommerce.topic" with routing key "order.placed"
                // - ProductService will consume this event to reduce stock
                // - NotificationService will consume this event to insert a notification
                // - correlationId (Guid.NewGuid().ToString()) helps trace this message across logs and services
                await _publisher.PublishOrderPlacedAsync(orderPlacedEvent, Guid.NewGuid().ToString());

                return true;
            }
            catch (Exception ex)
            {
                //Log the Exception
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        // Dummy implementations of missing methods so logic looks intact
        private string GenerateOrderNumberFromGuid(Guid id) => id.ToString();
        private Task<decimal> CalculateDiscountAmountAsync(IEnumerable<OrderItem> items) => Task.FromResult(0m);
        private Task<decimal> CalculateTaxAmountAsync(decimal val) => Task.FromResult(0m);
        private decimal CalculateShippingCharges(decimal val) => 0m;
    }
}
