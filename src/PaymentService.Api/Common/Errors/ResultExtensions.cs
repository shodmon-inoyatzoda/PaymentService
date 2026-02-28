using Microsoft.AspNetCore.Mvc;
using PaymentService.Domain.Common;

namespace PaymentService.Api.Common.Errors;

public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result)
    {
        if (result.IsSuccess)
            return Results.NoContent();

        return ToProblemResult(result.Error);
    }

    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return Results.Ok(result.Value);

        return ToProblemResult(result.Error);
    }

    public static IResult ToProblemResult(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation  => StatusCodes.Status400BadRequest,
            ErrorType.NotFound    => StatusCodes.Status404NotFound,
            ErrorType.Conflict    => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden   => StatusCodes.Status403Forbidden,
            _                     => StatusCodes.Status500InternalServerError,
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title  = GetTitle(error.Type),
            Detail = error.Message,
        };
        problemDetails.Extensions["errorCode"] = error.Code;

        return Results.Problem(problemDetails);
    }

    private static string GetTitle(ErrorType type) => type switch
    {
        ErrorType.Validation   => "Validation Error",
        ErrorType.NotFound     => "Not Found",
        ErrorType.Conflict     => "Conflict",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden    => "Forbidden",
        _                      => "Internal Server Error",
    };
}
