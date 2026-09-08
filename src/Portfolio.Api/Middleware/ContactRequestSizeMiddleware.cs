using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Configuration;

namespace Portfolio.Api.Middleware;

public sealed class ContactRequestSizeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IProblemDetailsService problemDetailsService)
    {
        if (!HttpMethods.IsPost(context.Request.Method) ||
            context.Request.Path != "/api/v1/contact")
        {
            await next(context);
            return;
        }

        if (context.Request.ContentLength > ContactRequestLimits.MaxBodySize)
        {
            await WritePayloadTooLargeAsync(context, problemDetailsService);
            return;
        }

        var originalBody = context.Request.Body;
        await using var bufferedBody = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await originalBody.ReadAsync(
                chunk.AsMemory(), context.RequestAborted);
            if (read == 0)
                break;
            if (bufferedBody.Length + read > ContactRequestLimits.MaxBodySize)
            {
                await WritePayloadTooLargeAsync(context, problemDetailsService);
                return;
            }

            await bufferedBody.WriteAsync(
                chunk.AsMemory(0, read), context.RequestAborted);
        }

        bufferedBody.Position = 0;
        context.Request.Body = bufferedBody;
        try
        {
            await next(context);
        }
        finally
        {
            context.Request.Body = originalBody;
        }
    }

    private static async Task WritePayloadTooLargeAsync(
        HttpContext context,
        IProblemDetailsService problemDetailsService)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        var problem = new ProblemDetails
        {
            Type = "https://httpstatuses.com/413",
            Title = "Payload too large",
            Status = StatusCodes.Status413PayloadTooLarge,
            Detail = "The Contact request body must not exceed 65536 bytes.",
            Instance = context.Request.Path,
        };
        problem.Extensions["requestId"] = context.TraceIdentifier;
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
        });
    }
}
