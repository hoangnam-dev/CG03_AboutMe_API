using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Authorization;
using Portfolio.Api.Models;
using Portfolio.Application.Profiles;

namespace Portfolio.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/portfolio/{slug}/profile")]
public sealed class ProfileController(IProfileService profileService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<PublicProfileResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<PublicProfileResponse>>> Get(
        string slug, [FromQuery] string? locale, CancellationToken cancellationToken)
    {
        var result = await profileService.GetPublicAsync(slug, locale, cancellationToken);
        return Ok(ApiResponse.Success(result));
    }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/admin/profile")]
public sealed class AdminProfileController(IProfileService profileService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<ProfileAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ProfileAdminResponse>>> Get(
        CancellationToken cancellationToken)
    {
        var result = await profileService.GetAdminAsync(cancellationToken);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPut]
    [ProducesResponseType<ApiResponse<ProfileAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ProfileAdminResponse>>> Put(
        [FromBody] ProfileUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await profileService.UpdateAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(result, "Profile updated."));
    }

    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ApiResponse<AvatarUploadResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<AvatarUploadResponse>>> UploadAvatar(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await profileService.ReplaceAvatarAsync(
            new ProfileMediaUpload(stream, file.FileName, file.ContentType, file.Length),
            cancellationToken);
        return Ok(ApiResponse.Success(new AvatarUploadResponse(result.Url), "Avatar replaced."));
    }

    [HttpPost("hero-image")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ApiResponse<HeroImageUploadResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<HeroImageUploadResponse>>> UploadHeroImage(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await profileService.ReplaceHeroImageAsync(
            new ProfileMediaUpload(stream, file.FileName, file.ContentType, file.Length),
            cancellationToken);
        return Ok(ApiResponse.Success(new HeroImageUploadResponse(result.Url), "Hero image replaced."));
    }
}
