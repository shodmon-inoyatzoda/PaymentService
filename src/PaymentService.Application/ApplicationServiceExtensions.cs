using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Auth;
using PaymentService.Application.Auth.Validators;

namespace PaymentService.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();

        // Register all validators from this assembly
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        return services;
    }
}
