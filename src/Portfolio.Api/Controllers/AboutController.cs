using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Authorization;
using Portfolio.Api.Models;
using Portfolio.Application.About;

namespace Portfolio.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/portfolio/{slug}/about")]
public sealed class AboutController(IAboutService aboutService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<PublicAboutResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<PublicAboutResponse>>> Get(
        string slug, [FromQuery] string? locale, CancellationToken cancellationToken)
    {
        var result = await aboutService.GetPublicAsync(slug, locale, cancellationToken);
        return Ok(ApiResponse.Success(result));
    }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/admin/about")]
public sealed class AdminAboutController(IAboutService aboutService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<AboutAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AboutAdminResponse>>> Get(
        CancellationToken cancellationToken)
    {
        var result = await aboutService.GetAdminAsync(cancellationToken);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPut]
    [ProducesResponseType<ApiResponse<AboutAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AboutAdminResponse>>> Put(
        [FromBody] AboutUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await aboutService.UpdateAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(result, "About content updated."));
    }
}
