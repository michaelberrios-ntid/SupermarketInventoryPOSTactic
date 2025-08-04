var builder = WebApplication.CreateBuilder(args);

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => "🏬 Store Inventory API is live");

// Add health check endpoint
app.MapHealthChecks("/health");

// Add API endpoints for inventory events (that the sync service expects)
app.MapPost("/api/inventory/events", async (HttpContext context) => 
{
    try
    {
        // Read the raw request body
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        
        // Log the received data for debugging
        Console.WriteLine($"🎯 Received inventory event: {body}");
        
        if (string.IsNullOrEmpty(body))
        {
            return Results.BadRequest(new { error = "Empty request body" });
        }
        
        // Accept inventory events from POS sync service
        return Results.Ok(new { success = true, message = "Event received", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error processing event: {ex.Message}");
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();
