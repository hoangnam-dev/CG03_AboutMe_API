using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Portfolio.Api.Authorization;
using Portfolio.Api.Configuration;
using Portfolio.Api.Errors;
using Portfolio.Infrastructure.Authentication;
using Portfolio.Infrastructure.Persistence;

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

        services.AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter()));
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
                    policy.WithOrigins(frontend.Value.Origin)
                        .AllowAnyHeader()
                        .AllowAnyMethod()));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                    ?? new JwtOptions();
                var signingKey = jwt.SigningKey;
                if (allowUnconfiguredDependencies && string.IsNullOrWhiteSpace(signingKey))
                {
                    signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                }

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.Email,
                    RoleClaimType = ClaimTypes.Role,
                };
                options.Events = new JwtBearerEvents
                {
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

        var healthChecks = services.AddHealthChecks();
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("PostgreSql")))
        {
            healthChecks.AddCheck(
                "postgresql",
                () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    "PostgreSQL is not configured."),
                tags: ["ready"]);
        }
        else
        {
            healthChecks.AddDbContextCheck<PortfolioDbContext>(
                "postgresql",
                tags: ["ready"]);
        }
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        return services;
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
