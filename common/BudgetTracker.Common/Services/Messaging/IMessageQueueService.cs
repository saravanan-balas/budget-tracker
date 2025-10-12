using BudgetTracker.Common.DTOs.Messaging;

namespace BudgetTracker.Common.Services.Messaging;

public interface IMessageQueueService
{
    Task PublishMessageAsync<T>(string queueName, T message) where T : class;
    Task<string> SubscribeAsync<T>(string queueName, Func<T, Task<bool>> handler, CancellationToken cancellationToken) where T : class;
    Task UnsubscribeAsync(string subscriptionId);
    Task<bool> IsQueueEmptyAsync(string queueName);
    Task<long> GetQueueLengthAsync(string queueName);
}

