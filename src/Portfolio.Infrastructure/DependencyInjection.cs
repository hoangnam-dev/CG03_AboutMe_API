using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Portfolio.Application.Common.Authentication;
using Portfolio.Application.Dashboard;
using Portfolio.Infrastructure.Authentication;
using Portfolio.Infrastructure.Availability;
using Portfolio.Infrastructure.Configuration;
using Portfolio.Infrastructure.Persistence;
using Portfolio.Infrastructure.Persistence.Repositories;

namespace Portfolio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool allowUnconfiguredDependencies = false)
    {
        var connectionString = configuration.GetConnectionString("PostgreSql") ?? string.Empty;
        var jwtSigningKey = configuration[$"{JwtOptions.SectionName}:SigningKey"] ?? string.Empty;
        var bootstrapEnabled = configuration.GetValue<bool>(
            $"{BootstrapAdminOptions.SectionName}:Enabled");
        var databaseRequired = !allowUnconfiguredDependencies || bootstrapEnabled;

        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString = connectionString);

        if (databaseRequired)
        {
            services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
            services.AddOptions<DatabaseOptions>().ValidateOnStart();
        }

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName));
        if (!allowUnconfiguredDependencies || !string.IsNullOrWhiteSpace(jwtSigningKey))
        {
            services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
            services.AddOptions<JwtOptions>().ValidateOnStart();
            services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        }
        else
        {
            services.AddSingleton<IAccessTokenIssuer, JwtUnavailableAccessTokenIssuer>();
        }

        services.AddSingleton<IValidateOptions<BootstrapAdminOptions>, BootstrapAdminOptionsValidator>();
        services.AddOptions<BootstrapAdminOptions>()
            .Bind(configuration.GetSection(BootstrapAdminOptions.SectionName))
            .ValidateOnStart();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<PortfolioDbContext>((provider, options) =>
                options.UseNpgsql(
                    provider.GetRequiredService<IOptions<DatabaseOptions>>()
                        .Value.ConnectionString));

            services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.Password.RequiredLength = 12;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    options.Lockout.AllowedForNewUsers = true;
                })
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<PortfolioDbContext>();

            services.AddScoped<IIdentityAuthenticator, IdentityAuthenticator>();
            services.AddScoped<IDashboardRepository, DashboardRepository>();
            services.AddScoped<AdminBootstrapper>();
        }
        else
        {
            services.AddScoped<IIdentityAuthenticator, DatabaseUnavailableIdentityAuthenticator>();
            services.AddScoped<IDashboardRepository, DatabaseUnavailableDashboardRepository>();
        }

        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
