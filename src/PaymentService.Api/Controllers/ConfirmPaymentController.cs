using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Auth.Interfaces;
using PaymentService.Application.Common;
using PaymentService.Application.Features.Payments.Commands.ConfirmPayment;
using PaymentService.Application.Features.Payments.DTOs;
using PaymentService.Application.Idempotency;
using PaymentService.Domain.Common;

namespace PaymentService.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public sealed class ConfirmPaymentController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _db;

    public ConfirmPaymentController(
        ISender sender,
        ICurrentUserService currentUserService,
        IApplicationDbContext db)
    {
        _sender = sender;
        _currentUserService = currentUserService;
        _db = db;
    }

    /// <summary>POST /api/payments/{paymentId}/confirm — confirm a pending payment</summary>
    [HttpPost("{paymentId:guid}/confirm")]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmPayment(
        [FromRoute] Guid paymentId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
            return Unauthorized();

        if (idempotencyKey is not null)
            return await ConfirmPaymentWithIdempotency(userId.Value, paymentId, idempotencyKey, cancellationToken);

        return await ExecuteConfirmPayment(userId.Value, paymentId, cancellationToken);
    }

    private async Task<IActionResult> ConfirmPaymentWithIdempotency(
        Guid userId,
        Guid paymentId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var requestHash = ComputeHash(userId, paymentId);

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

        var result = await _sender.Send(new ConfirmPaymentCommand(userId, paymentId), cancellationToken);
        if (result.IsFailure)
            return MapError(result.Error);

        var responseBody = JsonSerializer.Serialize(result.Value);
        var idempotencyRecord = IdempotencyKey.Create(
            userId,
            idempotencyKey,
            requestHash,
            StatusCodes.Status200OK,
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

        return Ok(result.Value);
    }

    private async Task<IActionResult> ExecuteConfirmPayment(
        Guid userId,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ConfirmPaymentCommand(userId, paymentId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : MapError(result.Error);
    }

    // Hash includes "confirm:" prefix to differentiate from create-payment keys stored with the same Idempotency-Key value
    private static string ComputeHash(Guid userId, Guid paymentId)
    {
        var input = $"confirm:{userId}:{paymentId}";
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
        ErrorType.ServiceUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, new { error.Code, error.Message }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { error.Code, error.Message })
    };
}
