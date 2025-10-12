using StackExchange.Redis;
using System.Text.Json;
using BudgetTracker.Common.DTOs.Messaging;
using Microsoft.Extensions.Logging;

namespace BudgetTracker.Common.Services.Messaging;

public class SimpleRedisMessageQueueService : IMessageQueueService, IDisposable
{
    private readonly IDatabase _database;
    private readonly ConnectionMultiplexer _connection;
    private readonly ILogger<SimpleRedisMessageQueueService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, CancellationTokenSource> _cancelTokens = new();

    public SimpleRedisMessageQueueService(string connectionString, ILogger<SimpleRedisMessageQueueService> logger)
    {
        _logger = logger;
        _connection = ConnectionMultiplexer.Connect(connectionString);
        _database = _connection.GetDatabase();
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        _logger.LogInformation("Simple Redis message queue service initialized");
    }

    public async Task PublishMessageAsync<T>(string queueName, T message) where T : class
    {
        try
        {
            var serializedMessage = JsonSerializer.Serialize(message, _jsonOptions);
            var queueKey = $"queue:{queueName}";
            
            await _database.ListLeftPushAsync(queueKey, serializedMessage);
            
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
            var cancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancelTokens[subscriptionId] = cancelTokenSource;
            
            // Start polling the queue
            _ = Task.Run(async () =>
            {
                await PollQueueAsync(queueName, subscriptionId, handler, cancelTokenSource.Token);
            }, cancelTokenSource.Token);
            
            _logger.LogInformation("Subscribed to queue {QueueName} with ID {SubscriptionId}", queueName, subscriptionId);
            
            return subscriptionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to queue {QueueName}", queueName);
            throw;
        }
    }

    private async Task PollQueueAsync<T>(string queueName, string subscriptionId, Func<T, Task<bool>> handler, CancellationToken cancellationToken) where T : class
    {
        var queueKey = $"queue:{queueName}";
        var processingQueueKey = $"processing:{queueName}";
        
        _logger.LogDebug("Starting queue polling for {QueueName}, subscription {SubscriptionId}", queueName, subscriptionId);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Move messages from main queue to processing queue atomically
                var message = await _database.ListRightPopLeftPushAsync(queueKey, processingQueueKey);
                
                if (!message.HasValue)
                {
                    // No messages, wait a bit before checking again
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    continue;
                }
                
                // Try to deserialize and process the message
                try
                {
                    var messageObject = JsonSerializer.Deserialize<T>(message!, _jsonOptions);
                    
                    if (messageObject != null)
                    {
                        var success = await handler(messageObject);
                        
                        if (success)
                        {
                            _logger.LogDebug("Successfully processed message from queue {QueueName}", queueName);
                        }
                        else
                        {
                            _logger.LogWarning("Message handler returned false for queue {QueueName}", queueName);
                        }
                        
                        // Remove from processing queue after successful processing
                        await _database.ListRemoveAsync(processingQueueKey, message, 1);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to deserialize message in queue {QueueName}", queueName);
                        await _database.ListRemoveAsync(processingQueueKey, message, 1);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Failed to deserialize message in queue {QueueName}: {Message}", queueName, message);
                    await _database.ListRemoveAsync(processingQueueKey, message, 1);
                }
                catch (Exception processEx)
                {
                    _logger.LogError(processEx, "Error processing message in queue {QueueName}", queueName);
                    await _database.ListRemoveAsync(processingQueueKey, message, 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling queue {QueueName}", queueName);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
        
        _logger.LogInformation("Stopped polling queue {QueueName} for subscription {SubscriptionId}", queueName, subscriptionId);
    }

    public async Task UnsubscribeAsync(string subscriptionId)
    {
        if (_cancelTokens.TryGetValue(subscriptionId, out var cancelToken))
        {
            cancelToken.Cancel();
            _cancelTokens.Remove(subscriptionId);
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

    public void Dispose()
    {
        foreach (var cancelToken in _cancelTokens.Values)
        {
            cancelToken.Cancel();
            cancelToken.Dispose();
        }
        
        _cancelTokens.Clear();
        _connection?.Dispose();
        
        _logger.LogInformation("Simple Redis message queue service disposed");
    }
}
