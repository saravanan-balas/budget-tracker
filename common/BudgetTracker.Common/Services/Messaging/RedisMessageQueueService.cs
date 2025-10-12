using StackExchange.Redis;
using System.Text.Json;
using BudgetTracker.Common.DTOs.Messaging;
using Microsoft.Extensions.Logging;

namespace BudgetTracker.Common.Services.Messaging;

public class RedisMessageQueueService : IMessageQueueService, IDisposable
{
    private readonly IDatabase _database;
    private readonly ConnectionMultiplexer _connection;
    private readonly ILogger<RedisMessageQueueService> _logger;
    private readonly Dictionary<string, ISubscriber> _subscribers = new();
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisMessageQueueService(string connectionString, ILogger<RedisMessageQueueService> logger)
    {
        _logger = logger;
        _connection = ConnectionMultiplexer.Connect(connectionString);
        _database = _connection.GetDatabase();
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        _logger.LogInformation("Redis message queue service initialized");
    }

    public async Task PublishMessageAsync<T>(string queueName, T message) where T : class
    {
        try
        {
            var serializedMessage = JsonSerializer.Serialize(message, _jsonOptions);
            var queueKey = $"queue:{queueName}";
            
            // Add timestamp for ordering
            var messageWithTimestamp = new
            {
                Message = serializedMessage,
                EnqueuedAt = DateTime.UtcNow,
                Priority = GetMessagePriority(message)
            };
            
            var fullMessage = JsonSerializer.Serialize(messageWithTimestamp, _jsonOptions);
            
            await _database.ListLeftPushAsync(queueKey, fullMessage);
            
            _logger.LogDebug("Published message to queue {QueueName}: {MessageType}", queueName, typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<string> SubscribeAsync<T>(string queueName, Func<T, Task<bool>> handler, CancellationToken cancellationToken) where T : class
    {
        var subscriptionId = Guid.NewGuid().ToString();
        
        try
        {
            var subscriber = _connection.GetSubscriber();
            var channelName = $"channel:{queueName}:{subscriptionId}";
            
            await subscriber.SubscribeAsync(RedisChannel.Literal(channelName), async (channel, message) =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                
                try
                {
                    // Parse the message envelope
                    using var document = JsonDocument.Parse(message!.ToString());
                    var root = document.RootElement;
                    
                    if (root.TryGetProperty("Message", out var messageElement))
                    {
                        var actualMessage = JsonSerializer.Deserialize<T>(messageElement.GetRawText(), _jsonOptions);
                        
                        if (actualMessage != null)
                        {
                            var success = await handler(actualMessage);
                            
                            if (!success)
                            {
                                _logger.LogWarning("Message handling failed for subscription {SubscriptionId}", subscriptionId);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Failed to deserialize message in subscription {SubscriptionId}", subscriptionId);
                        }
                    }
                    else
                    {
                        // Fallback: try to deserialize directly as the message type
                        var directMessage = JsonSerializer.Deserialize<T>(message!, _jsonOptions);
                        if (directMessage != null)
                        {
                            var success = await handler(directMessage);
                            if (!success)
                            {
                                _logger.LogWarning("Message handling failed for subscription {SubscriptionId}", subscriptionId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message in subscription {SubscriptionId}", subscriptionId);
                }
            });
            
            _subscribers[subscriptionId] = subscriber;
            
            // Start polling the queue
            _ = Task.Run(async () =>
            {
                await PollQueueAsync(queueName, channelName, cancellationToken);
            }, cancellationToken);
            
            _logger.LogInformation("Subscribed to queue {QueueName} with ID {SubscriptionId}", queueName, subscriptionId);
            
            return subscriptionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to queue {QueueName}", queueName);
            throw;
        }
    }

    private async Task PollQueueAsync(string queueName, string channelName, CancellationToken cancellationToken)
    {
        var queueKey = $"queue:{queueName}";
        var processingQueueKey = $"processing:{queueName}";
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Move messages from main queue to processing queue atomically
                var message = await _database.ListRightPopLeftPushAsync(queueKey, processingQueueKey);
                
                if (!message.HasValue)
                {
                    // No messages, wait a bit before checking again
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    continue;
                }
                
                // Send message to channel handler
                var subscriber = _connection.GetSubscriber();
                await subscriber.PublishAsync(RedisChannel.Literal(channelName), message!);
                
                // Remove from processing queue after successful processing
                await _database.ListRemoveAsync(processingQueueKey, message, 1);
                
                _logger.LogDebug("Processed message from queue {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling queue {QueueName}", queueName);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    public async Task UnsubscribeAsync(string subscriptionId)
    {
        if (_subscribers.TryGetValue(subscriptionId, out var subscriber))
        {
            await subscriber.UnsubscribeAllAsync();
            _subscribers.Remove(subscriptionId);
            _logger.LogInformation("Unsubscribed from subscription {SubscriptionId}", subscriptionId);
        }
    }

    public async Task<bool> IsQueueEmptyAsync(string queueName)
    {
        var queueKey = $"queue:{queueName}";
        var length = await _database.ListLengthAsync(queueKey);
        return length == 0;
    }

    public async Task<long> GetQueueLengthAsync(string queueName)
    {
        var queueKey = $"queue:{queueName}";
        return await _database.ListLengthAsync(queueKey);
    }

    private static int GetMessagePriority<T>(T message) where T : class
    {
        return message switch
        {
            ImportProcessingMessage msg => msg.Priority,
            RecurringTransactionDetectionMessage msg => msg.Priority,
            MerchantOptimizationMessage msg => msg.Priority,
            CategoryOptimizationMessage msg => msg.Priority,
            _ => 0
        };
    }

    public void Dispose()
    {
        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.UnsubscribeAllAsync().Wait();
        }
        
        _subscribers.Clear();
        _connection?.Dispose();
        
        _logger.LogInformation("Redis message queue service disposed");
    }
}
