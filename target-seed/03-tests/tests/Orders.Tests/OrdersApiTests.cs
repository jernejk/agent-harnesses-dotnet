using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Orders.Tests;

public class OrdersApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData(1001)]
    [InlineData(1002)]
    [InlineData(1003)]
    public async Task Order_totals_are_available(int orderId)
    {
        var response = await factory.CreateClient().GetAsync($"/orders/{orderId}/total");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_orders_return_404()
    {
        var response = await factory.CreateClient().GetAsync("/orders/9999/total");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
