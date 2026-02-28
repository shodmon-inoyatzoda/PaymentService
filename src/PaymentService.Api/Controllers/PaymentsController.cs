using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Auth.Interfaces;
using PaymentService.Application.Common;
using PaymentService.Application.Features.Payments.Commands.CreatePayment;
using PaymentService.Application.Features.Payments.DTOs;
using PaymentService.Application.Features.Payments.Queries.GetPaymentsByOrder;
using PaymentService.Application.Idempotency;
using PaymentService.Domain.Common;

namespace PaymentService.Api.Controllers;

[ApiController]
[Route("api/orders/{orderId:guid}/payments")]
[Authorize]
public sealed class PaymentsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _db;

    public PaymentsController(
        ISender sender,
        ICurrentUserService currentUserService,
        IApplicationDbContext db)
    {
        _sender = sender;
        _currentUserService = currentUserService;
        _db = db;
    }

    /// <summary>POST /api/orders/{orderId}/payments — initiate payment</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreatePayment(
        [FromRoute] Guid orderId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
            return Unauthorized();

        if (idempotencyKey is not null)
            return await CreatePaymentWithIdempotency(userId.Value, orderId, idempotencyKey, cancellationToken);

        return await ExecuteCreatePayment(userId.Value, orderId, cancellationToken);
    }

    /// <summary>GET /api/orders/{orderId}/payments — list payments by order</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayments(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
            return Unauthorized();

        var result = await _sender.Send(new GetPaymentsByOrderQuery(userId.Value, orderId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error);
    }

    private async Task<IActionResult> CreatePaymentWithIdempotency(
        Guid userId,
        Guid orderId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var requestHash = ComputeHash(userId, orderId);

        var existing = await _db.IdempotencyKeys
            .FirstOrDefaultAsync(
                k => k.UserId == userId && k.Key == idempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                return Conflict(new { code = "Idempotency.Conflict", message = "Idempotency key was already used with a different request payload." });

            var stored = JsonSerializer.Deserialize<PaymentDto>(existing.ResponseBody);
            return StatusCode(existing.ResponseStatusCode, stored);
        }

        var result = await _sender.Send(new CreatePaymentCommand(userId, orderId), cancellationToken);
        if (result.IsFailure)
            return MapError(result.Error);

        var responseBody = JsonSerializer.Serialize(result.Value);
        var idempotencyRecord = IdempotencyKey.Create(
            userId,
            idempotencyKey,
            requestHash,
            StatusCodes.Status201Created,
            responseBody,
            DateTimeOffset.UtcNow.AddDays(1));

        _db.IdempotencyKeys.Add(idempotencyRecord);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Concurrent request with the same key won the race; return the stored response.
            var concurrent = await _db.IdempotencyKeys
                .FirstOrDefaultAsync(
                    k => k.UserId == userId && k.Key == idempotencyKey,
                    cancellationToken);

            if (concurrent is not null)
            {
                if (concurrent.RequestHash != requestHash)
                    return Conflict(new { code = "Idempotency.Conflict", message = "Idempotency key was already used with a different request payload." });

                var stored = JsonSerializer.Deserialize<PaymentDto>(concurrent.ResponseBody);
                return StatusCode(concurrent.ResponseStatusCode, stored);
            }

            throw;
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    private async Task<IActionResult> ExecuteCreatePayment(
        Guid userId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreatePaymentCommand(userId, orderId), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : MapError(result.Error);
    }

    private static string ComputeHash(Guid userId, Guid orderId)
    {
        var input = $"{userId}:{orderId}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private IActionResult MapError(Error error) => error.Type switch
    {
        ErrorType.Validation => BadRequest(new { error.Code, error.Message }),
        ErrorType.NotFound => NotFound(new { error.Code, error.Message }),
        ErrorType.Conflict => Conflict(new { error.Code, error.Message }),
        ErrorType.Unauthorized => Unauthorized(new { error.Code, error.Message }),
        ErrorType.Forbidden => Forbid(),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { error.Code, error.Message })
    };
}
