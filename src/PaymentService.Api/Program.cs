using FluentValidation;
using PaymentService.Api.Endpoints;
using PaymentService.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, configuration) => configuration
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName());

    // Add services to the container.
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();

    // Register Infrastructure (EF Core + PostgreSQL)
    builder.Services.AddInfrastructure(builder.Configuration);

    // Register FluentValidation validators from Api assembly
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    // Global exception handling — converts unhandled exceptions to ProblemDetails
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();

    app.MapHealthEndpoints();
    app.MapSampleEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
