var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "🏬 Store Inventory API is live");

app.Run();
