using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Portfolio.Application.About;
using Portfolio.Application.Authentication;
using Portfolio.Application.Common.Authentication;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Dashboard;
using Portfolio.Application.Profiles;
using Portfolio.Application.Skills;
using Portfolio.Infrastructure.Authentication;
using Portfolio.Infrastructure.Availability;
using Portfolio.Infrastructure.Configuration;
using Portfolio.Infrastructure.Persistence;
using Portfolio.Infrastructure.Persistence.Repositories;
using Portfolio.Infrastructure.Storage;

namespace Portfolio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool allowUnconfiguredDependencies = false)
    {
        var connectionString = configuration.GetConnectionString("PostgreSql") ?? string.Empty;
        var jwtCertificatePath = configuration[$"{JwtOptions.SectionName}:SigningCertificatePath"] ?? string.Empty;
        var bootstrapEnabled = configuration.GetValue<bool>(
            $"{BootstrapAdminOptions.SectionName}:Enabled");
        var databaseRequired = !allowUnconfiguredDependencies || bootstrapEnabled;
        var storageUrl = configuration[$"{SupabaseStorageOptions.SectionName}:Url"];
        var storageServiceKey = configuration[$"{SupabaseStorageOptions.SectionName}:ServiceRoleKey"];
        var storageConfigured =
            !string.IsNullOrWhiteSpace(storageUrl) ||
            !string.IsNullOrWhiteSpace(storageServiceKey);

        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString = connectionString);

        if (databaseRequired)
        {
            services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
            services.AddOptions<DatabaseOptions>().ValidateOnStart();
        }

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName));
        if (!allowUnconfiguredDependencies || !string.IsNullOrWhiteSpace(jwtCertificatePath))
        {
            services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
            services.AddOptions<JwtOptions>().ValidateOnStart();
            services.AddSingleton<JwtKeyRing>();
            services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        }
        else
        {
            services.AddSingleton(_ => JwtKeyRing.CreateEphemeral());
            services.AddSingleton<IAccessTokenIssuer, JwtUnavailableAccessTokenIssuer>();
        }

        services.AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetSection(RefreshTokenOptions.SectionName));
        if (!allowUnconfiguredDependencies || !string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IValidateOptions<RefreshTokenOptions>, RefreshTokenOptionsValidator>();
            services.AddOptions<RefreshTokenOptions>().ValidateOnStart();
        }
        services.AddSingleton<IRefreshTokenProtector, RefreshTokenProtector>();
        services.AddSingleton<ICsrfTokenService, SessionCsrfTokenService>();
        services.AddSingleton<IAuthSessionLifetime, AuthSessionLifetime>();

        services.AddSingleton<IValidateOptions<BootstrapAdminOptions>, BootstrapAdminOptionsValidator>();
        services.AddOptions<BootstrapAdminOptions>()
            .Bind(configuration.GetSection(BootstrapAdminOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<SupabaseStorageOptions>()
            .Bind(configuration.GetSection(SupabaseStorageOptions.SectionName));
        if (!allowUnconfiguredDependencies || storageConfigured)
        {
            services.AddSingleton<
                IValidateOptions<SupabaseStorageOptions>,
                SupabaseStorageOptionsValidator>();
            services.AddOptions<SupabaseStorageOptions>().ValidateOnStart();
            services.AddHttpClient<SupabaseFileStorage>(ConfigureStorageClient);
            services.AddHttpClient<SupabaseStorageHealthCheck>(ConfigureStorageClient);
            services.AddScoped<IFileStorage>(provider =>
                provider.GetRequiredService<SupabaseFileStorage>());
        }
        else
        {
            services.AddScoped<IFileStorage, StorageUnavailableFileStorage>();
        }

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
            services.AddScoped<IAuthSessionRepository, AuthSessionRepository>();
            services.AddScoped<IAccessSessionValidator, AccessSessionValidator>();
            services.AddScoped<IDashboardRepository, DashboardRepository>();
            services.AddScoped<IProfileRepository, ProfileRepository>();
            services.AddScoped<IAboutRepository, AboutRepository>();
            services.AddScoped<ISkillRepository, SkillRepository>();
            services.AddScoped<AdminBootstrapper>();
        }
        else
        {
            services.AddScoped<IIdentityAuthenticator, DatabaseUnavailableIdentityAuthenticator>();
            services.AddScoped<IAuthSessionRepository, DatabaseUnavailableAuthSessionRepository>();
            services.AddScoped<IAccessSessionValidator, DatabaseUnavailableAccessSessionValidator>();
            services.AddScoped<IDashboardRepository, DatabaseUnavailableDashboardRepository>();
            services.AddScoped<IProfileRepository, DatabaseUnavailableProfileRepository>();
            services.AddScoped<IAboutRepository, DatabaseUnavailableAboutRepository>();
            services.AddScoped<ISkillRepository, DatabaseUnavailableSkillRepository>();
        }

        services.AddSingleton(TimeProvider.System);
        return services;
    }

    private static void ConfigureStorageClient(
        IServiceProvider provider,
        HttpClient client)
    {
        var storage = provider.GetRequiredService<IOptions<SupabaseStorageOptions>>().Value;
        client.BaseAddress = storage.Url;
        client.Timeout = TimeSpan.FromSeconds(storage.RequestTimeoutSeconds);
    }
}
