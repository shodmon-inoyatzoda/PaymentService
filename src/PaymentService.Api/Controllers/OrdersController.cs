using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Contracts;
using PaymentService.Application.Auth.Interfaces;
using PaymentService.Application.Features.Orders.Commands.CreateOrder;
using PaymentService.Application.Features.Orders.DTOs;
using PaymentService.Application.Features.Orders.Queries.GetOrderById;
using PaymentService.Domain.Common;

namespace PaymentService.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateOrderRequest> _createOrderValidator;

    public OrdersController(
        ISender sender,
        ICurrentUserService currentUserService,
        IValidator<CreateOrderRequest> createOrderValidator)
    {
        _sender = sender;
        _currentUserService = currentUserService;
        _createOrderValidator = createOrderValidator;
    }

    /// <summary>POST /api/orders</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
            return Unauthorized();

        var validation = await _createOrderValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return ValidationProblem(validation);

        var command = new CreateOrderCommand(userId.Value, request.Amount, request.Currency);
        var result = await _sender.Send(command, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetOrderById), new { id = result.Value.Id }, result.Value)
            : MapError(result.Error);
    }

    /// <summary>GET /api/orders/{id}</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
            return Unauthorized();

        var query = new GetOrderByIdQuery(id, userId.Value);
        var result = await _sender.Send(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error);
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

    private IActionResult ValidationProblem(FluentValidation.Results.ValidationResult validation)
    {
        var errors = validation.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return ValidationProblem(new ValidationProblemDetails(errors));
    }
}
