namespace POS.SyncService.Configuration;

/// <summary>
/// Configuration options for the sync service
/// </summary>
public class SyncOptions
{
    public const string SectionName = "Sync";

    /// <summary>
    /// Unique identifier for this store
    /// </summary>
    public string StoreId { get; set; } = string.Empty;

    /// <summary>
    /// Central API endpoint for sending transactions
    /// </summary>
    public string CentralApiEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Health check endpoint for the central API
    /// </summary>
    public string HealthCheckEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of retry attempts for failed syncs
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay before retrying failed syncs (in minutes)
    /// </summary>
    public int RetryDelayMinutes { get; set; } = 5;

    /// <summary>
    /// Interval between sync runs (in seconds)
    /// </summary>
    public int SyncIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Number of records to process in each batch
    /// </summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// HTTP timeout for API calls (in seconds)
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to enable detailed logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// How often to run cleanup operations (in hours)
    /// </summary>
    public int CleanupIntervalHours { get; set; } = 24;

    /// <summary>
    /// How many days of data to keep during cleanup
    /// </summary>
    public int DataRetentionDays { get; set; } = 30;

    /// <summary>
    /// Whether to automatically create the database if it doesn't exist
    /// </summary>
    public bool AutoCreateDatabase { get; set; } = true;

    /// <summary>
    /// Connection string for the local SQLite database
    /// </summary>
    public string LocalDatabaseConnectionString { get; set; } = "Data Source=data/POS_Local.db";

    /// <summary>
    /// Additional headers to include in API requests
    /// </summary>
    public Dictionary<string, string> ApiHeaders { get; set; } = new();

    /// <summary>
    /// Whether to sync transactions
    /// </summary>
    public bool EnableTransactionSync { get; set; } = true;

    /// <summary>
    /// Whether to sync inventory updates
    /// </summary>
    public bool EnableInventorySync { get; set; } = true;

    /// <summary>
    /// Maximum age of transactions to sync (in days)
    /// </summary>
    public int MaxTransactionAgeDays { get; set; } = 7;

    /// <summary>
    /// Whether to enable health checks
    /// </summary>
    public bool EnableHealthChecks { get; set; } = true;

    /// <summary>
    /// Health check interval (in minutes)
    /// </summary>
    public int HealthCheckIntervalMinutes { get; set; } = 5;
}

/// <summary>
/// Kafka configuration for publishing events
/// </summary>
public class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>
    /// Kafka bootstrap servers
    /// </summary>
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>
    /// Topic for inventory events
    /// </summary>
    public string InventoryTopic { get; set; } = "inventory-events";

    /// <summary>
    /// Whether to enable Kafka publishing
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Kafka producer configuration
    /// </summary>
    public Dictionary<string, string> ProducerConfig { get; set; } = new();
}

/// <summary>
/// Database configuration options
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// SQLite connection string
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=data/POS_Local.db";

    /// <summary>
    /// Whether to enable detailed database logging
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Database command timeout (in seconds)
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to automatically migrate the database on startup
    /// </summary>
    public bool AutoMigrate { get; set; } = true;

    /// <summary>
    /// Whether to enable connection pooling
    /// </summary>
    public bool EnableConnectionPooling { get; set; } = true;

    /// <summary>
    /// Maximum number of database connections in the pool
    /// </summary>
    public int MaxPoolSize { get; set; } = 10;
}
