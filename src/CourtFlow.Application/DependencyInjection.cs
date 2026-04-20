using System.Reflection;
using CourtFlow.Application.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CourtFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCourtFlowApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
