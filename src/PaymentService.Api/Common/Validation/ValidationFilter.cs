using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace PaymentService.Api.Common.Validation;

public class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        if (request is null)
            return await next(context);

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

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
                Extensions =
                {
                    ["errors"] = errors
                }
            };

            return Results.Problem(problem);
        }

        return await next(context);
    }
}
