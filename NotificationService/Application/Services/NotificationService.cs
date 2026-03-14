using NotificationService.Contracts.Interfaces;
using NotificationService.Application.Interfaces;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Services
{
    public class NotificationService : INotificationService, INotificationProcessor
    {
        public Task CreateAsync(CreateNotificationRequestDTO request)
        {
            return Task.CompletedTask;
        }

        public Task ProcessQueueBatchAsync(int take, int skip)
        {
            return Task.CompletedTask;
        }
    }
}
