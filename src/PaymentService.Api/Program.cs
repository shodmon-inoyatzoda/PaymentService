using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PaymentService.Api.Auth;
using PaymentService.Api.Endpoints;
using PaymentService.Application;
using PaymentService.Application.Auth.Interfaces;
using PaymentService.Infrastructure;
using PaymentService.Infrastructure.Auth;
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

    // Register Application layer (use-cases, validators)
    builder.Services.AddApplication();

// Current user service reads from HttpContext
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Controllers
    builder.Services.AddControllers();

// JWT Authentication
    var jwtSettings = builder.Configuration
                          .GetSection(JwtSettings.SectionName)
                          .Get<JwtSettings>()
                      ?? throw new InvalidOperationException("JwtSettings section is not configured.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorization();

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
