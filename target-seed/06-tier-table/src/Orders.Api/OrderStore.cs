namespace Orders.Api;

public sealed class OrderStore
{
    readonly Dictionary<int, Customer> _customers = new()
    {
        [1] = new(1, "Ava Thompson", "gold"),
        [2] = new(2, "Noah Patel", "standard"),
        [3] = new(3, "Mia Chen", "silver"),
    };

    readonly Dictionary<int, Order> _orders = new()
    {
        [1001] = new(1001, 2, [new("KB-101", 1, 129.00m), new("MS-220", 2, 24.50m)]),
        [1002] = new(1002, 1, [new("MN-270", 1, 349.00m)]),
        [1003] = new(1003, 3, [new("HD-310", 3, 19.90m)]),
    };

    public Order? FindOrder(int id) => _orders.GetValueOrDefault(id);

    public Customer? FindCustomer(int id) => _customers.GetValueOrDefault(id);
}
