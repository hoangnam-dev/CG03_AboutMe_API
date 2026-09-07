using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Portfolio.Api.Authentication;
using Portfolio.Api.Models;
using Portfolio.Application.Authentication;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Infrastructure.Authentication;

namespace Portfolio.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IAuthService authService,
    AuthCookieWriter cookieWriter,
    IOptions<RefreshTokenOptions> refreshOptions) : ControllerBase
{
    public const string CsrfHeaderName = "X-CSRF-Token";
    private readonly RefreshTokenOptions _refreshOptions = refreshOptions.Value;

    [AllowAnonymous]
    [EnableRateLimiting("AuthLogin")]
    [HttpPost("login")]
    [ProducesResponseType<ApiResponse<LoginResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, GetClientContext(), cancellationToken);
        cookieWriter.Write(
            Response,
            result.RefreshToken,
            result.Response.CsrfToken,
            result.RefreshTokenExpiresAt);
        SetNoStore(Response);
        return Ok(ApiResponse.Success(result.Response, "Signed in."));
    }

    [AllowAnonymous]
    [EnableRateLimiting("AuthRefresh")]
    [HttpPost("refresh")]
    [ProducesResponseType<ApiResponse<LoginResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh(
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await authService.RefreshAsync(
                Request.Cookies[_refreshOptions.CookieName] ?? string.Empty,
                Request.Headers[CsrfHeaderName].ToString(),
                GetClientContext(),
                cancellationToken);
            cookieWriter.Write(
                Response,
                result.RefreshToken,
                result.Response.CsrfToken,
                result.RefreshTokenExpiresAt);
            SetNoStore(Response);
            return Ok(ApiResponse.Success(result.Response, "Token refreshed."));
        }
        catch (AuthenticationFailedException)
        {
            cookieWriter.Delete(Response);
            return Unauthorized(new ProblemDetails
            {
                Type = "https://httpstatuses.com/401",
                Title = "Authentication failed",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "Invalid authentication credentials.",
                Instance = Request.Path,
                Extensions = { ["requestId"] = HttpContext.TraceIdentifier },
            });
        }
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(Request.Headers[CsrfHeaderName].ToString(), cancellationToken);
        cookieWriter.Delete(Response);
        SetNoStore(Response);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        await authService.LogoutAllAsync(Request.Headers[CsrfHeaderName].ToString(), cancellationToken);
        cookieWriter.Delete(Response);
        SetNoStore(Response);
        return NoContent();
    }

    [Authorize]
    [HttpGet("sessions")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AuthSessionResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuthSessionResponse>>>> Sessions(
        CancellationToken cancellationToken)
    {
        var result = await authService.GetSessionsAsync(cancellationToken);
        SetNoStore(Response);
        return Ok(ApiResponse.Success(result));
    }

    [Authorize]
    [HttpDelete("sessions/{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RevokeSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await authService.RevokeSessionAsync(
            sessionId,
            Request.Headers[CsrfHeaderName].ToString(),
            cancellationToken);
        if (Request.HttpContext.User.FindFirst("sid")?.Value == sessionId.ToString())
        {
            cookieWriter.Delete(Response);
        }

        SetNoStore(Response);
        return NoContent();
    }

    private AuthClientContext GetClientContext() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        Request.Headers.UserAgent.ToString());

    private static void SetNoStore(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers.Pragma = "no-cache";
    }
}
