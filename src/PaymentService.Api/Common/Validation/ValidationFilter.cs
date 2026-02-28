using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace PaymentService.Api.Common.Validation;

public class ValidationFilter<TRequest> : IEndpointFilter
{
    private readonly IValidator<TRequest> _validator;

    public ValidationFilter(IValidator<TRequest> validator)
    {
        _validator = validator;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        if (request is null)
            return await next(context);

        var result = await _validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title  = "Validation Error",
                Detail = "One or more validation errors occurred.",
            };
            problem.Extensions["errors"] = errors;

            return Results.Problem(problem);
        }

        return await next(context);
    }
}
