using PaymentService.Api.Common.Errors;
using PaymentService.Api.Common.Validation;
using PaymentService.Api.Contracts.Sample;
using PaymentService.Domain.Common;

namespace PaymentService.Api.Endpoints;

public static class SampleEndpoints
{
    public static IEndpointRouteBuilder MapSampleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sample").WithTags("Sample");

        group.MapPost("/", HandleCreate)
            .AddEndpointFilter<ValidationFilter<CreateSampleRequest>>()
            .WithName("CreateSample")
            .WithSummary("Sample endpoint demonstrating validation and Result mapping.");

        return app;
    }

    private static IResult HandleCreate(CreateSampleRequest request)
    {
        // Demonstrates Result→IResult mapping for each error type.
        // In real endpoints this would call an application use-case.
        if (request.Name.Equals("conflict", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<CreateSampleRequest>(
                Error.Conflict("Sample.Conflict", "A sample with this name already exists.")).ToHttpResult();

        if (request.Name.Equals("notfound", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<CreateSampleRequest>(
                Error.NotFound("Sample.NotFound", "The requested sample was not found.")).ToHttpResult();

        var result = Result.Success(new { request.Name, request.Amount, request.Currency });
        return result.ToHttpResult();
    }
}
