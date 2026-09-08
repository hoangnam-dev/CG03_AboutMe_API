using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Portfolio.Api.Authentication;
using Portfolio.Api.Authorization;
using Portfolio.Api.Configuration;
using Portfolio.Api.Errors;
using Portfolio.Api.OpenApi;
using Portfolio.Application.Certificates;
using Portfolio.Application.Common.Authentication;
using Portfolio.Application.Profiles;
using Portfolio.Application.Projects;
using Portfolio.Application.Resumes;
using Portfolio.Application.Skills;
using Portfolio.Infrastructure.Authentication;
using Portfolio.Infrastructure.Persistence;
using Portfolio.Infrastructure.Storage;

namespace Portfolio.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        bool allowUnconfiguredDependencies = false)
    {
        services.AddSingleton<IValidateOptions<FrontendOptions>, FrontendOptionsValidator>();
        services.AddOptions<FrontendOptions>()
            .Bind(configuration.GetSection(FrontendOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<UploadOptions>, UploadOptionsValidator>();
        services.AddOptions<UploadOptions>()
            .Bind(configuration.GetSection(UploadOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AuthRateLimitOptions>, AuthRateLimitOptionsValidator>();
        services.AddOptions<AuthRateLimitOptions>()
            .Bind(configuration.GetSection(AuthRateLimitOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton(provider =>
        {
            var uploads = provider.GetRequiredService<IOptions<UploadOptions>>().Value;
            var bucket = configuration[$"{SupabaseStorageOptions.SectionName}:Buckets:Avatars"]
                ?? "avatars";
            return new ProfileMediaSettings(
                bucket,
                uploads.MaxFileSize,
                uploads.MaxHeroImageWidth,
                uploads.MaxHeroImageHeight);
        });
        services.AddSingleton(provider =>
        {
            var uploads = provider.GetRequiredService<IOptions<UploadOptions>>().Value;
            var bucket = configuration[$"{SupabaseStorageOptions.SectionName}:Buckets:ProjectImages"]
                ?? "project-images";
            return new ProjectImageSettings(
                bucket,
                uploads.MaxFileSize,
                uploads.MaxProjectGalleryFiles);
        });
        services.AddSingleton(provider =>
        {
            var uploads = provider.GetRequiredService<IOptions<UploadOptions>>().Value;
            var bucket = configuration[$"{SupabaseStorageOptions.SectionName}:Buckets:CertificateFiles"]
                ?? "certificate-files";
            return new CertificateEvidenceSettings(
                bucket,
                uploads.MaxFileSize,
                TimeSpan.FromMinutes(5));
        });
        services.AddSingleton(provider =>
        {
            var uploads = provider.GetRequiredService<IOptions<UploadOptions>>().Value;
            var bucket = configuration[$"{SupabaseStorageOptions.SectionName}:Buckets:CvFiles"]
                ?? "cv-files";
            return new ResumeSettings(
                bucket,
                uploads.MaxFileSize,
                TimeSpan.FromMinutes(5));
        });
        services.AddSingleton<IValidateOptions<SkillIconOptions>, SkillIconOptionsValidator>();
        services.AddOptions<SkillIconOptions>()
            .Bind(configuration.GetSection(SkillIconOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton(provider =>
        {
            var icons = provider.GetRequiredService<IOptions<SkillIconOptions>>().Value;
            var bucket = configuration[$"{SupabaseStorageOptions.SectionName}:Buckets:SkillIcons"]
                ?? "skill-icons";
            var storageUrl = configuration[$"{SupabaseStorageOptions.SectionName}:Url"];
            var storageHost = Uri.TryCreate(storageUrl, UriKind.Absolute, out var parsedStorageUrl)
                ? parsedStorageUrl.Host
                : null;
            return new SkillIconSettings(
                bucket,
                icons.MaxFileSize,
                icons.AllowedExternalHosts.ToHashSet(StringComparer.OrdinalIgnoreCase),
                storageHost);
        });

        services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter(
                        System.Text.Json.JsonNamingPolicy.CamelCase)));
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problem = new ValidationProblemDetails(context.ModelState)
                {
                    Type = "https://httpstatuses.com/400",
                    Title = "Validation failed",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "One or more validation errors occurred.",
                    Instance = context.HttpContext.Request.Path,
                };
                problem.Extensions["requestId"] = context.HttpContext.TraceIdentifier;
                return new BadRequestObjectResult(problem)
                {
                    ContentTypes = { "application/problem+json" },
                };
            };
        });

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["requestId"] =
                    context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddCors();
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<FrontendOptions>>((options, frontend) =>
                options.AddPolicy("Frontend", policy =>
                    policy.WithOrigins(frontend.Value.Origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtKeyRing, IOptions<JwtOptions>>((options, keyRing, configuredJwt) =>
            {
                var jwt = configuredJwt.Value;

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = (_, _, keyId, _) =>
                        keyId is not null && keyRing.ValidationKeys.TryGetValue(keyId, out var key)
                            ? [key]
                            : [],
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidTypes = ["JWT"],
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                    NameClaimType = ClaimTypes.Email,
                    RoleClaimType = ClaimTypes.Role,
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateAccessSessionAsync,
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await WriteAuthorizationProblemAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "Unauthorized",
                            "A valid bearer token is required.");
                    },
                    OnForbidden = context => WriteAuthorizationProblemAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "Forbidden",
                        "The authenticated user is not allowed to access this resource."),
                };
            });
        services.AddAuthorizationBuilder()
            .AddPolicy(
                AuthorizationPolicies.Admin,
                policy => policy.RequireRole(AdminBootstrapper.AdminRole));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddScoped<AuthCookieWriter>();
        services.AddRateLimiter(options =>
        {
            var configured = configuration.GetSection(AuthRateLimitOptions.SectionName)
                .Get<AuthRateLimitOptions>() ?? new AuthRateLimitOptions();
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("AuthLogin", context => CreateIpPartition(
                context,
                configured.LoginPermitLimit,
                configured.WindowSeconds));
            options.AddPolicy("AuthRefresh", context => CreateIpPartition(
                context,
                configured.RefreshPermitLimit,
                configured.WindowSeconds));
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                await WriteAuthorizationProblemAsync(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    "Too many requests",
                    "Too many authentication attempts.");
            };
        });

        var healthChecks = services.AddHealthChecks();
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("PostgreSql")))
        {
            healthChecks.AddCheck(
                "postgresql",
                () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    "PostgreSQL is not configured."),
                tags: ["ready", "supabase"]);
        }
        else
        {
            healthChecks.AddDbContextCheck<PortfolioDbContext>(
                "postgresql",
                tags: ["ready", "supabase"]);
        }

        var storageUrl = configuration[$"{SupabaseStorageOptions.SectionName}:Url"];
        var storageServiceKey =
            configuration[$"{SupabaseStorageOptions.SectionName}:ServiceRoleKey"];
        if (!string.IsNullOrWhiteSpace(storageUrl) &&
            !string.IsNullOrWhiteSpace(storageServiceKey))
        {
            healthChecks.AddCheck<SupabaseStorageHealthCheck>(
                "supabase-storage",
                tags: ["ready", "supabase"]);
        }
        else
        {
            healthChecks.AddCheck(
                "supabase-storage",
                () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    "Supabase Storage is not configured."),
                tags: ["ready", "supabase"]);
        }
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            const string bearerScheme = "Bearer";
            options.AddSecurityDefinition(bearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Enter the access token returned by POST /api/v1/auth/login.",
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(bearerScheme, document)] = [],
            });
            options.OperationFilter<AllowAnonymousOperationFilter>();
            options.OperationFilter<RequestExampleOperationFilter>();
        });
        return services;
    }

    private static RateLimitPartition<string> CreateIpPartition(
        HttpContext context,
        int permitLimit,
        int windowSeconds) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            });

    private static async Task ValidateAccessSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId) ||
            !Guid.TryParse(principal?.FindFirst("sid")?.Value, out var sessionId) ||
            string.IsNullOrWhiteSpace(principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value) ||
            !long.TryParse(
                principal?.FindFirst(JwtRegisteredClaimNames.Iat)?.Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out _) ||
            principal?.FindFirst(ClaimTypes.Role) is null ||
            !int.TryParse(
                principal?.FindFirst("auth_version")?.Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var authVersion))
        {
            context.Fail("Required access-token claims are missing.");
            return;
        }

        var validator = context.HttpContext.RequestServices
            .GetRequiredService<IAccessSessionValidator>();
        if (!await validator.IsValidAsync(
                new CurrentAuthSession(userId, sessionId, authVersion),
                context.HttpContext.RequestAborted))
        {
            context.Fail("The authentication session is no longer active.");
        }
    }

    private static async Task WriteAuthorizationProblemAsync(
        HttpContext httpContext,
        int status,
        string title,
        string detail)
    {
        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{status}",
            Title = title,
            Status = status,
            Detail = detail,
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["requestId"] = httpContext.TraceIdentifier;

        var problemDetailsService =
            httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }
}
