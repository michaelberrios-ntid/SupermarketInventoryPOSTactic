var builder = WebApplication.CreateBuilder(args);

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => "🏬 Store Inventory API is live");

// Add health check endpoint
app.MapHealthChecks("/health");

// Add API endpoints for inventory events (that the sync service expects)
app.MapPost("/api/inventory/events", (object eventData) => 
{
    // Accept inventory events from POS sync service
    return Results.Ok(new { success = true, message = "Event received" });
});

app.Run();
