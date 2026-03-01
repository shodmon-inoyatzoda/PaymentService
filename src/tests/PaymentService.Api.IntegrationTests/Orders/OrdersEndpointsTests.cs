using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PaymentService.Api.Contracts;
using PaymentService.Api.IntegrationTests.Helpers;
using PaymentService.Api.IntegrationTests.Infrastructure;
using PaymentService.Application.Features.Orders.DTOs;
using PaymentService.Domain.Enums.Orders;

namespace PaymentService.Api.IntegrationTests.Orders;

[Collection("Integration")]
public sealed class OrdersEndpointsTests
{
    private readonly PaymentServiceWebApplicationFactory _factory;

    public OrdersEndpointsTests(PaymentServiceWebApplicationFactory factory) =>
        _factory = factory;

    // ── Create → Get ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrder_Returns201_And_GetOrder_Returns_SameOrder()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAndLoginAsync(
            client, "+15560000001", "orders1@test.com");
        AuthHelper.SetBearerToken(client, auth.AccessToken);

        // Create
        var createResponse = await client.PostAsJsonAsync(
            "/api/orders", new CreateOrderRequest(150.00m, "USD"));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createResponse.Content.ReadFromJsonAsync<OrderDto>())!;
        created.Amount.Should().Be(150.00m);
        created.Currency.Should().Be("USD");
        created.Status.Should().Be(OrderStatus.Created);

        // Get by id
        var getResponse = await client.GetAsync($"/api/orders/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = (await getResponse.Content.ReadFromJsonAsync<OrderDto>())!;
        fetched.Id.Should().Be(created.Id);
        fetched.UserId.Should().Be(created.UserId);
        fetched.Amount.Should().Be(created.Amount);
    }

    [Fact]
    public async Task GetOrder_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Cross-user isolation ───────────────────────────────────────────────

    [Fact]
    public async Task GetOrder_ForOtherUsersOrder_Returns404()
    {
        // User A creates an order
        var clientA = _factory.CreateClient();
        var authA = await AuthHelper.RegisterAndLoginAsync(
            clientA, "+15560000002", "ordersA@test.com");
        AuthHelper.SetBearerToken(clientA, authA.AccessToken);

        var createResponse = await clientA.PostAsJsonAsync(
            "/api/orders", new CreateOrderRequest(99.00m, "EUR"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var orderA = (await createResponse.Content.ReadFromJsonAsync<OrderDto>())!;

        // User B attempts to fetch User A's order
        var clientB = _factory.CreateClient();
        var authB = await AuthHelper.RegisterAndLoginAsync(
            clientB, "+15560000003", "ordersB@test.com");
        AuthHelper.SetBearerToken(clientB, authB.AccessToken);

        var getResponse = await clientB.GetAsync($"/api/orders/{orderA.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Validation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrder_WithNegativeAmount_Returns400()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAndLoginAsync(
            client, "+15560000004", "orders_invalid@test.com");
        AuthHelper.SetBearerToken(client, auth.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/orders", new CreateOrderRequest(-1m, "USD"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
