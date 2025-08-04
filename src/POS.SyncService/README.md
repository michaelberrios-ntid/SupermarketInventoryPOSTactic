# POS Sync Service

A robust background synchronization service implementing **Modifiability** and **Availability** tactics for Point of Sale (POS) systems. This service ensures reliable data synchronization between local SQLite databases and central store systems.

## 🏗️ Architecture Overview

The sync service implements several architectural tactics:

### Modifiability Tactics
- **Layered Architecture**: Clear separation between data, business logic, and presentation layers
- **Dependency Injection**: Loose coupling between components
- **Configuration-driven**: External configuration for easy deployment variations
- **Interface Segregation**: Well-defined interfaces for testability and flexibility

### Availability Tactics
- **Retry Mechanisms**: Exponential backoff with configurable retry limits
- **Circuit Breaker**: Prevents cascading failures when central services are down
- **Health Checks**: Continuous monitoring of service and dependency health
- **Graceful Degradation**: Continues local operations even when sync fails
- **Error Recovery**: Comprehensive error logging and recovery strategies

## 🚀 Features

- ✅ **Reliable Sync**: Synchronizes POS transactions and inventory updates
- ✅ **Retry Logic**: Up to 3 configurable retry attempts with exponential backoff
- ✅ **Batch Processing**: Processes records in configurable batches for efficiency
- ✅ **Health Monitoring**: Built-in health checks for all components
- ✅ **Kafka Integration**: Optional event publishing to Kafka topics
- ✅ **Circuit Breaker**: Protects against cascading failures
- ✅ **Data Retention**: Automatic cleanup of old logs and data
- ✅ **Container-Ready**: Docker support with proper configuration management

## 📊 Data Flow

```
Local SQLite DB (Synced=false) 
    ↓
Background Service (every 10s)
    ↓
HTTP API Call (with retries)
    ↓
Central Store Database
    ↓
Update Local DB (Synced=true)
    ↓
Optional: Publish to Kafka
```

## 🛠️ Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `STORE_ID` | Unique store identifier | `STORE001` |
| `KAFKA_BOOTSTRAP_SERVERS` | Kafka server endpoints | `kafka:9092` |
| `CENTRAL_API_URL` | Central API endpoint | `http://store_api:8080` |

### Configuration Sections

#### Sync Options
```json
{
  "Sync": {
    "StoreId": "STORE001",
    "CentralApiEndpoint": "http://store_api:8080/api/inventory/events",
    "MaxRetryAttempts": 3,
    "RetryDelayMinutes": 5,
    "SyncIntervalSeconds": 10,
    "BatchSize": 50,
    "EnableDetailedLogging": false
  }
}
```

#### Database Options
```json
{
  "Database": {
    "ConnectionString": "Data Source=data/POS_Local.db",
    "AutoMigrate": true,
    "CommandTimeoutSeconds": 30
  }
}
```

#### Kafka Options
```json
{
  "Kafka": {
    "BootstrapServers": "kafka:9092",
    "InventoryTopic": "inventory-events",
    "Enabled": true
  }
}
```

## 🗄️ Database Schema

### PosTransaction
- **Id**: Unique transaction identifier
- **StoreId**: Store identifier
- **ProductId**: Product identifier
- **TransactionType**: Sale, Purchase, Adjustment, Return, etc.
- **Quantity**: Transaction quantity
- **UnitPrice**: Price per unit
- **TotalAmount**: Total transaction amount
- **Synced**: Synchronization status
- **SyncAttempts**: Number of sync attempts
- **LastSyncError**: Last error message

### InventoryUpdate
- **Id**: Unique update identifier
- **StoreId**: Store identifier
- **ProductId**: Product identifier
- **PreviousStock**: Stock before update
- **NewStock**: Stock after update
- **UpdateType**: Type of inventory change
- **Synced**: Synchronization status

### SyncRetryLog
- **EntityType**: Type of entity (Transaction/Inventory)
- **EntityId**: ID of the entity
- **AttemptNumber**: Retry attempt number
- **ErrorMessage**: Error details
- **ShouldRetry**: Whether to retry again

## 🔧 Usage

### Running Locally

```bash
cd src/POS.SyncService
dotnet restore
dotnet run
```

### Running with Docker

```bash
docker build -t pos-sync-service .
docker run -d \
  -e STORE_ID=STORE001 \
  -e KAFKA_BOOTSTRAP_SERVERS=kafka:9092 \
  -v ./data:/app/data \
  pos-sync-service
```

### Sample Data Creation

The service includes a data seeder for testing:

```csharp
// Create sample transactions and inventory updates
await dataSeeder.SeedSampleDataAsync(transactionCount: 10, inventoryUpdateCount: 5);
```

## 📈 Monitoring

### Health Checks

Health checks are available at `/health` and include:
- Database connectivity
- Central API reachability
- Kafka connectivity (if enabled)
- Sync service status
- Pending item counts

### Metrics

The service tracks:
- Total transactions synced
- Total inventory updates synced
- Failed sync attempts
- Pending items count
- Average sync duration

### Logging

Structured logging with different levels:
- **Information**: Normal operation events
- **Warning**: Sync failures and retries
- **Error**: Unrecoverable errors
- **Debug**: Detailed operation logs (development)

## 🔄 Retry Strategy

The service implements a sophisticated retry strategy:

1. **Immediate Retry**: First failure triggers immediate retry
2. **Exponential Backoff**: Subsequent retries use exponential delays (2^attempt seconds)
3. **Maximum Attempts**: Configurable limit (default: 3 attempts)
4. **Circuit Breaker**: Stops retries when API is consistently failing
5. **Error Logging**: All failures are logged with context

## 🎯 Tactics Implementation

### Modifiability Tactics

1. **Separation of Concerns**
   - Data Access Layer: `PosDbContext` and models
   - Business Logic Layer: `ISyncService` implementation
   - Configuration Layer: Options pattern with validation

2. **Dependency Injection**
   - All services registered in DI container
   - Interfaces for easy testing and mocking
   - Configuration through Options pattern

3. **Event-Driven Architecture**
   - Kafka integration for real-time events
   - Async operations throughout
   - Loose coupling between components

### Availability Tactics

1. **Fault Detection**
   - Health checks for all dependencies
   - Continuous monitoring of sync operations
   - Automatic error detection and logging

2. **Fault Recovery**
   - Retry mechanisms with exponential backoff
   - Circuit breaker to prevent cascade failures
   - Graceful degradation when services are unavailable

3. **Fault Prevention**
   - Input validation at all entry points
   - Resource management and connection pooling
   - Proper error handling and logging

## 🧪 Testing

### Unit Testing
```bash
dotnet test
```

### Integration Testing
The service includes integration tests for:
- Database operations
- HTTP API calls
- Kafka publishing
- Health checks

### Load Testing
Batch processing ensures the service can handle high volumes:
- Configurable batch sizes
- Memory-efficient processing
- Connection pooling

## 🐳 Docker Support

### Dockerfile Features
- Multi-stage build for optimized image size
- Non-root user for security
- Health check endpoint
- Proper signal handling for graceful shutdown

### Docker Compose Integration
The service integrates seamlessly with the existing docker-compose setup:
- Automatic dependency resolution
- Shared volumes for data persistence
- Network connectivity to Kafka and central API

## 📝 Best Practices

1. **Error Handling**: Comprehensive error handling with context
2. **Logging**: Structured logging with correlation IDs
3. **Configuration**: Environment-specific configurations
4. **Security**: Non-root containers, secure defaults
5. **Performance**: Batch processing, connection pooling
6. **Monitoring**: Health checks, metrics, alerting
7. **Maintenance**: Automatic data cleanup, log rotation

## 🔮 Future Enhancements

- [ ] **Database Sharding**: Support for multiple database connections
- [ ] **Event Sourcing**: Complete audit trail of all changes
- [ ] **CQRS**: Separate read/write models for better performance
- [ ] **Real-time Dashboard**: Web UI for monitoring sync status
- [ ] **Advanced Metrics**: Prometheus/Grafana integration
- [ ] **Auto-scaling**: Kubernetes HPA support

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License.
