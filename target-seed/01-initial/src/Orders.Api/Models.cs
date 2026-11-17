namespace Orders.Api;

public sealed record Customer(int Id, string Name, string Tier);

public sealed record OrderLine(string Sku, int Quantity, decimal UnitPrice);

public sealed record Order(int Id, int CustomerId, IReadOnlyList<OrderLine> Lines);
