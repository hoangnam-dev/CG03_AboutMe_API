using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Application.Common.Exceptions;

namespace Portfolio.Api.Errors;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ValidationException => (
                StatusCodes.Status400BadRequest,
                "Validation failed",
                exception.Message),
            AuthenticationFailedException => (
                StatusCodes.Status401Unauthorized,
                "Authentication failed",
                exception.Message),
            ForbiddenException => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                exception.Message),
            NotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                exception.Message),
            ConflictException => (
                StatusCodes.Status409Conflict,
                "Conflict",
                exception.Message),
            ServiceUnavailableException => (
                StatusCodes.Status503ServiceUnavailable,
                "Service unavailable",
                exception.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Server error",
                "An unexpected error occurred."),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            GlobalExceptionHandlerLog.Unhandled(logger, exception);
        }

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
        if (exception is ValidationException validationException)
        {
            problem.Extensions["errors"] = validationException.Errors;
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}

internal static partial class GlobalExceptionHandlerLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing the request")]
    public static partial void Unhandled(ILogger logger, Exception exception);
}
