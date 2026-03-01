using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PaymentService.Api.Contracts;
using PaymentService.Api.IntegrationTests.Helpers;
using PaymentService.Api.IntegrationTests.Infrastructure;
using PaymentService.Application.Features.Orders.DTOs;
using PaymentService.Application.Features.Payments.DTOs;
using PaymentService.Domain.Enums.Orders;
using PaymentService.Domain.Enums.Payments;

namespace PaymentService.Api.IntegrationTests.Payments;

[Collection("Integration")]
public sealed class PaymentsEndpointsTests
{
    private readonly PaymentServiceWebApplicationFactory _factory;

    public PaymentsEndpointsTests(PaymentServiceWebApplicationFactory factory) =>
        _factory = factory;

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an authenticated user, places an order and returns both the
    /// client (with bearer token set) and the created order.
    /// </summary>
    private async Task<(HttpClient client, OrderDto order)> SetupOrderAsync(
        string phone, string email)
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAndLoginAsync(client, phone, email);
        AuthHelper.SetBearerToken(client, auth.AccessToken);

        var createOrderResponse = await client.PostAsJsonAsync(
            "/api/orders", new CreateOrderRequest(200.00m, "USD"));
        createOrderResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = (await createOrderResponse.Content.ReadFromJsonAsync<OrderDto>())!;
        return (client, order);
    }

    // ── Create payment ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePayment_Returns201_WithPendingStatus()
    {
        var (client, order) = await SetupOrderAsync("+15570000001", "pay1@test.com");

        var response = await client.PostAsync(
            $"/api/orders/{order.Id}/payments", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payment = (await response.Content.ReadFromJsonAsync<PaymentDto>())!;
        payment.OrderId.Should().Be(order.Id);
        payment.Amount.Should().Be(order.Amount);
        payment.Status.Should().Be(PaymentStatus.Pending);
    }

    // ── Idempotent create ──────────────────────────────────────────────────

    [Fact]
    public async Task CreatePayment_SameIdempotencyKey_ReturnsSamePayment()
    {
        var (client, order) = await SetupOrderAsync("+15570000002", "pay2@test.com");
        var key = Guid.NewGuid().ToString();

        var req1 = AuthHelper.BuildRequestWithIdempotencyKey(
            HttpMethod.Post, $"/api/orders/{order.Id}/payments", key);
        var response1 = await client.SendAsync(req1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        var payment1 = (await response1.Content.ReadFromJsonAsync<PaymentDto>())!;

        var req2 = AuthHelper.BuildRequestWithIdempotencyKey(
            HttpMethod.Post, $"/api/orders/{order.Id}/payments", key);
        var response2 = await client.SendAsync(req2);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);
        var payment2 = (await response2.Content.ReadFromJsonAsync<PaymentDto>())!;

        payment2.Id.Should().Be(payment1.Id);
    }

    // ── List payments ──────────────────────────────────────────────────────

    [Fact]
    public async Task ListPayments_Returns_PaymentsForOrder()
    {
        var (client, order) = await SetupOrderAsync("+15570000003", "pay3@test.com");

        // Initiate a payment
        await client.PostAsync($"/api/orders/{order.Id}/payments", null);

        var listResponse = await client.GetAsync($"/api/orders/{order.Id}/payments");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payments = (await listResponse.Content.ReadFromJsonAsync<List<PaymentDto>>())!;
        payments.Should().ContainSingle(p => p.OrderId == order.Id);
    }

    [Fact]
    public async Task ListPayments_ForNonExistentOrder_Returns404()
    {
        var client = _factory.CreateClient();
        var auth = await AuthHelper.RegisterAndLoginAsync(
            client, "+15570000004", "pay4@test.com");
        AuthHelper.SetBearerToken(client, auth.AccessToken);

        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}/payments");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Confirm payment ────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmPayment_Returns200_And_OrderBecomesPaid()
    {
        var (client, order) = await SetupOrderAsync("+15570000005", "pay5@test.com");

        // Initiate
        var createResp = await client.PostAsync($"/api/orders/{order.Id}/payments", null);
        var payment = (await createResp.Content.ReadFromJsonAsync<PaymentDto>())!;

        // Confirm
        var confirmResp = await client.PostAsync(
            $"/api/payments/{payment.Id}/confirm", null);

        confirmResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = (await confirmResp.Content.ReadFromJsonAsync<PaymentDto>())!;
        confirmed.Status.Should().Be(PaymentStatus.Successful);

        // Order should now be Paid
        var orderResp = await client.GetAsync($"/api/orders/{order.Id}");
        var updatedOrder = (await orderResp.Content.ReadFromJsonAsync<OrderDto>())!;
        updatedOrder.Status.Should().Be(OrderStatus.Paid);
    }

    // ── Confirm idempotency ────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmPayment_SameIdempotencyKey_ReturnsSameResult()
    {
        var (client, order) = await SetupOrderAsync("+15570000006", "pay6@test.com");

        // Initiate payment
        var createResp = await client.PostAsync($"/api/orders/{order.Id}/payments", null);
        var payment = (await createResp.Content.ReadFromJsonAsync<PaymentDto>())!;

        var key = Guid.NewGuid().ToString();

        var req1 = AuthHelper.BuildRequestWithIdempotencyKey(
            HttpMethod.Post, $"/api/payments/{payment.Id}/confirm", key);
        var resp1 = await client.SendAsync(req1);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed1 = (await resp1.Content.ReadFromJsonAsync<PaymentDto>())!;

        var req2 = AuthHelper.BuildRequestWithIdempotencyKey(
            HttpMethod.Post, $"/api/payments/{payment.Id}/confirm", key);
        var resp2 = await client.SendAsync(req2);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed2 = (await resp2.Content.ReadFromJsonAsync<PaymentDto>())!;

        confirmed2.Id.Should().Be(confirmed1.Id);
        confirmed2.Status.Should().Be(PaymentStatus.Successful);
    }
}
