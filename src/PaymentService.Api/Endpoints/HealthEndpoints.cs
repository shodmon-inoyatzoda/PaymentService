namespace PaymentService.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTimeOffset.UtcNow }))
            .WithName("GetHealth")
            .WithTags("Health")
            .WithSummary("Returns the health status of the API.");

        return app;
    }
}
