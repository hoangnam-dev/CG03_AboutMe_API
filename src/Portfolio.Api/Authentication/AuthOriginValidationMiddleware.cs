using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Portfolio.Api.Configuration;

namespace Portfolio.Api.Authentication;

public sealed class AuthOriginValidationMiddleware(
    RequestDelegate next,
    IOptions<FrontendOptions> options)
{
    private readonly HashSet<string> _allowedOrigins =
        new(options.Value.Origins, StringComparer.Ordinal);

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetailsService)
    {
        if (context.Request.Path.StartsWithSegments(
                "/api/v1/auth",
                StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
        }

        if (!RequiresOriginValidation(context.Request) || IsAllowed(context.Request.Headers.Origin))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        var problem = new ProblemDetails
        {
            Type = "https://httpstatuses.com/403",
            Title = "Forbidden",
            Status = StatusCodes.Status403Forbidden,
            Detail = "The request origin is not allowed.",
            Instance = context.Request.Path,
        };
        problem.Extensions["requestId"] = context.TraceIdentifier;
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
        });
    }

    private bool IsAllowed(Microsoft.Extensions.Primitives.StringValues origins) =>
        origins.Count == 1 &&
        origins[0] is { } origin &&
        _allowedOrigins.Contains(origin);

    private static bool RequiresOriginValidation(HttpRequest request) =>
        (HttpMethods.IsPost(request.Method) || HttpMethods.IsDelete(request.Method)) &&
        request.Path.StartsWithSegments("/api/v1/auth", StringComparison.OrdinalIgnoreCase);
}
