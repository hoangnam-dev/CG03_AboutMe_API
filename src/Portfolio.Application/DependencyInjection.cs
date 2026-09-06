using Microsoft.Extensions.DependencyInjection;
using Portfolio.Application.Authentication;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Dashboard;

namespace Portfolio.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<StorageReplacement>();
        return services;
    }
}
