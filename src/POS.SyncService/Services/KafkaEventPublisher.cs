using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.SyncService.Configuration;
using POS.SyncService.Models;
using System.Text.Json;

namespace POS.SyncService.Services;

/// <summary>
/// Service for publishing events to Kafka for real-time notifications
/// </summary>
public interface IKafkaEventPublisher
{
    Task PublishTransactionEventAsync(PosTransaction transaction, CancellationToken cancellationToken = default);
    Task PublishInventoryUpdateEventAsync(InventoryUpdate inventoryUpdate, CancellationToken cancellationToken = default);
    Task<bool> IsKafkaHealthyAsync();
}

/// <summary>
/// Kafka event publisher implementation with error handling and health monitoring
/// </summary>
public class KafkaEventPublisher : IKafkaEventPublisher, IDisposable
{
    private readonly ILogger<KafkaEventPublisher> _logger;
    private readonly KafkaOptions _kafkaOptions;
    private readonly SyncOptions _syncOptions;
    private readonly IProducer<string, string>? _producer;
    private bool _disposed = false;

    public KafkaEventPublisher(
        ILogger<KafkaEventPublisher> logger,
        IOptions<KafkaOptions> kafkaOptions,
        IOptions<SyncOptions> syncOptions)
    {
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
        _syncOptions = syncOptions.Value;

        if (_kafkaOptions.Enabled && !string.IsNullOrEmpty(_kafkaOptions.BootstrapServers))
        {
            var config = new ProducerConfig
            {
                BootstrapServers = _kafkaOptions.BootstrapServers,
                Acks = Acks.Leader,
                EnableIdempotence = true,
                MaxInFlight = 1,
                MessageTimeoutMs = 30000,
                RequestTimeoutMs = 30000,
            };

            // Add custom producer configuration
            foreach (var kvp in _kafkaOptions.ProducerConfig)
            {
                config.Set(kvp.Key, kvp.Value);
            }

            _producer = new ProducerBuilder<string, string>(config)
                .SetErrorHandler((_, e) => _logger.LogError("Kafka producer error: {Error}", e.Reason))
                .SetLogHandler((_, logMessage) => 
                {
                    switch (logMessage.Level)
                    {
                        case SyslogLevel.Emergency:
                        case SyslogLevel.Alert:
                        case SyslogLevel.Critical:
                        case SyslogLevel.Error:
                            _logger.LogError("Kafka: {Message}", logMessage.Message);
                            break;
                        case SyslogLevel.Warning:
                            _logger.LogWarning("Kafka: {Message}", logMessage.Message);
                            break;
                        case SyslogLevel.Info:
                        case SyslogLevel.Notice:
                            _logger.LogInformation("Kafka: {Message}", logMessage.Message);
                            break;
                        default:
                            _logger.LogDebug("Kafka: {Message}", logMessage.Message);
                            break;
                    }
                })
                .Build();

            _logger.LogInformation("Kafka producer initialized for {BootstrapServers}", _kafkaOptions.BootstrapServers);
        }
        else
        {
            _logger.LogInformation("Kafka publishing is disabled");
        }
    }

    public async Task PublishTransactionEventAsync(PosTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (!_kafkaOptions.Enabled || _producer == null)
        {
            return;
        }

        try
        {
            var eventMessage = new
            {
                EventType = "TransactionSynced",
                Timestamp = DateTime.UtcNow,
                StoreId = transaction.StoreId,
                TransactionId = transaction.TransactionId,
                ProductId = transaction.ProductId,
                TransactionType = transaction.TransactionType,
                Quantity = transaction.Quantity,
                UnitPrice = transaction.UnitPrice,
                TotalAmount = transaction.TotalAmount,
                TransactionDate = transaction.TransactionDate,
                SyncedAt = transaction.SyncedAt
            };

            var json = JsonSerializer.Serialize(eventMessage);
            var message = new Message<string, string>
            {
                Key = $"{transaction.StoreId}_{transaction.ProductId}",
                Value = json,
                Headers = new Headers()
            };
            
            message.Headers.Add("store-id", System.Text.Encoding.UTF8.GetBytes(transaction.StoreId));
            message.Headers.Add("event-type", System.Text.Encoding.UTF8.GetBytes("transaction"));
            message.Headers.Add("timestamp", System.Text.Encoding.UTF8.GetBytes(transaction.TransactionDate.ToString("O")));

            var result = await _producer.ProduceAsync(_kafkaOptions.InventoryTopic, message, cancellationToken);
            
            _logger.LogDebug("Published transaction event to Kafka: {Topic} partition {Partition} offset {Offset}", 
                result.Topic, result.Partition.Value, result.Offset.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish transaction event to Kafka for transaction {TransactionId}", 
                transaction.TransactionId);
        }
    }

    public async Task PublishInventoryUpdateEventAsync(InventoryUpdate inventoryUpdate, CancellationToken cancellationToken = default)
    {
        if (!_kafkaOptions.Enabled || _producer == null)
        {
            return;
        }

        try
        {
            var eventMessage = new
            {
                EventType = "InventoryUpdateSynced",
                Timestamp = DateTime.UtcNow,
                StoreId = inventoryUpdate.StoreId,
                UpdateId = inventoryUpdate.Id,
                ProductId = inventoryUpdate.ProductId,
                UpdateType = inventoryUpdate.UpdateType,
                QuantityChange = inventoryUpdate.QuantityChange,
                Reason = inventoryUpdate.Reason,
                UpdateDate = inventoryUpdate.UpdateDate,
                SyncedAt = inventoryUpdate.SyncedAt
            };

            var json = JsonSerializer.Serialize(eventMessage);
            var message = new Message<string, string>
            {
                Key = $"{inventoryUpdate.StoreId}_{inventoryUpdate.ProductId}",
                Value = json,
                Headers = new Headers()
            };
            
            message.Headers.Add("store-id", System.Text.Encoding.UTF8.GetBytes(inventoryUpdate.StoreId));
            message.Headers.Add("event-type", System.Text.Encoding.UTF8.GetBytes("inventory-update"));
            message.Headers.Add("timestamp", System.Text.Encoding.UTF8.GetBytes(inventoryUpdate.UpdateDate.ToString("O")));

            var result = await _producer.ProduceAsync(_kafkaOptions.InventoryTopic, message, cancellationToken);
            
            _logger.LogDebug("Published inventory event to Kafka: {Topic} partition {Partition} offset {Offset}", 
                result.Topic, result.Partition.Value, result.Offset.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish inventory event to Kafka for update {UpdateId}", 
                inventoryUpdate.Id);
        }
    }

    public async Task<bool> IsKafkaHealthyAsync()
    {
        if (!_kafkaOptions.Enabled || _producer == null)
        {
            return true; // If Kafka is disabled, consider it "healthy"
        }

        try
        {
            // Simple way to check if producer is working - try to get producer name
            var producerName = _producer.Name;
            return !string.IsNullOrEmpty(producerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka health check failed");
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _producer?.Flush(TimeSpan.FromSeconds(10));
                _producer?.Dispose();
                _logger.LogInformation("Kafka producer disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Kafka producer");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
