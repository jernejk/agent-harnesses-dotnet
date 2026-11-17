using Orders.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddSimpleConsole(o => o.TimestampFormat = "HH:mm:ss ");
builder.Services.AddSingleton<OrderStore>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "1.3.0" }));

app.MapGet("/orders/{id:int}", (int id, OrderStore store) =>
    store.FindOrder(id) is { } order ? Results.Ok(order) : Results.NotFound());

app.MapGet("/orders/{id:int}/total", (int id, OrderStore store, ILogger<Program> log) =>
{
    log.LogInformation("Order {OrderId} total requested", id);
    if (store.FindOrder(id) is not { } order)
    {
        log.LogWarning("Order {OrderId} not found", id);
        return Results.NotFound();
    }
    var customer = store.FindCustomer(order.CustomerId)!;
    return Results.Ok(new OrderTotal(id, Pricing.Total(order, customer), "AUD"));
});

app.Run();

public partial class Program;
