using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Auth.Validators;
using PaymentService.Application.Features.Auth.Commands.Register;

namespace PaymentService.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<RegisterCommandHandler>());

        // Register all validators from this assembly
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        return services;
    }
}
