using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Common.Errors;
using PaymentService.Domain.Common;

namespace PaymentService.Api.Tests.Common.Errors;

public class ResultExtensionsTests
{
    [Fact]
    public void ToHttpResult_WhenSuccessResult_ReturnsNoContent()
    {
        var result = Result.Success();

        var httpResult = result.ToHttpResult();

        httpResult.Should().BeOfType<NoContent>();
    }

    [Fact]
    public void ToHttpResult_WhenSuccessResultOfT_ReturnsOkWithValue()
    {
        var result = Result.Success(42);

        var httpResult = result.ToHttpResult();

        httpResult.Should().BeOfType<Ok<int>>();
        ((Ok<int>)httpResult).Value.Should().Be(42);
    }

    [Theory]
    [InlineData(ErrorType.Validation,   StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.NotFound,     StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict,     StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.Forbidden,    StatusCodes.Status403Forbidden)]
    [InlineData(ErrorType.Failure,      StatusCodes.Status500InternalServerError)]
    public void ToProblemResult_MapsErrorTypeToCorrectStatusCode(ErrorType errorType, int expectedStatusCode)
    {
        var error = errorType switch
        {
            ErrorType.Validation   => Error.Validation("Test.Validation", "Validation failed"),
            ErrorType.NotFound     => Error.NotFound("Test.NotFound", "Not found"),
            ErrorType.Conflict     => Error.Conflict("Test.Conflict", "Conflict"),
            ErrorType.Unauthorized => Error.Unauthorized("Test.Unauthorized", "Unauthorized"),
            ErrorType.Forbidden    => Error.Forbidden("Test.Forbidden", "Forbidden"),
            _                      => Error.Failure("Test.Failure", "Failure"),
        };

        var httpResult = ResultExtensions.ToProblemResult(error);

        var problemResult = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problemResult.ProblemDetails.Status.Should().Be(expectedStatusCode);
        problemResult.ProblemDetails.Detail.Should().Be(error.Message);
        problemResult.ProblemDetails.Extensions["errorCode"].Should().Be(error.Code);
    }

    [Fact]
    public void ToHttpResult_WhenFailureResult_ReturnsProblemWithCorrectStatus()
    {
        var error = Error.NotFound("Item.NotFound", "Item was not found");
        var result = Result.Failure(error);

        var httpResult = result.ToHttpResult();

        var problemResult = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problemResult.ProblemDetails.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ToHttpResult_WhenFailureResultOfT_ReturnsProblemWithCorrectStatus()
    {
        var error = Error.Conflict("Item.Conflict", "Item already exists");
        var result = Result.Failure<int>(error);

        var httpResult = result.ToHttpResult();

        var problemResult = httpResult.Should().BeOfType<ProblemHttpResult>().Subject;
        problemResult.ProblemDetails.Status.Should().Be(StatusCodes.Status409Conflict);
    }
}
