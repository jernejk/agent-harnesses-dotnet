namespace Orders.Api;

public static class Pricing
{
    public const decimal GstRate = 0.10m;

    public static decimal Total(Order order, Customer customer)
    {
        var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
        var discount = customer.Tier == "gold" ? 0.05m : 0m;
        var total = subtotal * (1 - discount) * (1 + GstRate);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
}
