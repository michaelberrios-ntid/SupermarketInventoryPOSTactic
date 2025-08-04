using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using POS.SyncService;
using POS.SyncService.Configuration;
using POS.SyncService.Data;
using POS.SyncService.Services;
using Polly;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);
builder.Configuration.AddEnvironmentVariables();

// Configure options
builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection(SyncOptions.SectionName));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

// Add database context
builder.Services.AddDbContext<PosDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? builder.Configuration["Database:ConnectionString"] 
        ?? "Data Source=/app/data/POS_Local.db";
    
    options.UseSqlite(connectionString);
    
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Add HTTP client with Polly
builder.Services.AddHttpClient<ISyncService, POS.SyncService.Services.SyncService>(client =>
{
    var syncOptions = builder.Configuration.GetSection(SyncOptions.SectionName).Get<SyncOptions>();
    if (syncOptions != null)
    {
        client.Timeout = TimeSpan.FromSeconds(syncOptions.HttpTimeoutSeconds);
        
        // Add any custom headers
        foreach (var header in syncOptions.ApiHeaders)
        {
            client.DefaultRequestHeaders.Add(header.Key, header.Value);
        }
    }
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

// Add services
builder.Services.AddScoped<ISyncService, POS.SyncService.Services.SyncService>();
builder.Services.AddScoped<IDataSeeder, DataSeeder>();
builder.Services.AddSingleton<POS.SyncService.Services.IKafkaEventPublisher, POS.SyncService.Services.KafkaEventPublisher>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PosDbContext>("database")
    .AddCheck<SyncServiceHealthCheck>("sync-service");

// Add the worker service
builder.Services.AddHostedService<Worker>();

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

var host = builder.Build();

// Ensure data directory exists
var dataDir = Path.Combine("/app", "data");
if (!Directory.Exists(dataDir))
{
    Directory.CreateDirectory(dataDir);
    Console.WriteLine($"📁 Created data directory: {dataDir}");
}

Console.WriteLine("🔄 POS Sync Service with Modifiability & Availability Tactics");
Console.WriteLine("📊 Features: Retry policies, circuit breakers, health checks, batch processing");

await host.RunAsync();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return Policy
        .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .Or<HttpRequestException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                Console.WriteLine($"🔄 HTTP Retry {retryCount} in {timespan.TotalSeconds}s");
            });
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return Policy
        .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .Or<HttpRequestException>()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (result, timespan) =>
            {
                Console.WriteLine($"⚡ Circuit breaker opened for {timespan.TotalSeconds}s");
            },
            onReset: () =>
            {
                Console.WriteLine("✅ Circuit breaker closed");
            });
}
