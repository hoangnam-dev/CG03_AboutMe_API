using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Authorization;
using Portfolio.Api.Models;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Resumes;

namespace Portfolio.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/portfolio/{slug}/cv")]
public sealed class ResumesController(IResumeService resumeService) : ControllerBase
{
    [HttpGet("current")]
    [ProducesResponseType<ApiResponse<ResumePublicResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<ResumePublicResponse>>> GetCurrent(
        string slug,
        [FromQuery] string? language,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(await resumeService.GetPublicCurrentAsync(
            slug, language, cancellationToken)));
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/admin/cv")]
public sealed class AdminResumesController(IResumeService resumeService) : ControllerBase
{
    private static readonly IReadOnlySet<string> RejectedVersionFields =
        new HashSet<string>(
            ["version", "versionYear", "versionSequence"],
            StringComparer.OrdinalIgnoreCase);

    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ResumeAdminResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ResumeAdminResponse>>>> Get(
        [FromQuery] string? language = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await resumeService.GetResumesAsync(
            new ResumeAdminQuery(language, page, pageSize), cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<ResumeAdminResponse>>(
            result.Items,
            "Success",
            new PaginationMeta(result.Page, result.PageSize, result.Total, result.TotalPages)));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ApiResponse<ResumeAdminResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<ApiResponse<ResumeAdminResponse>>> Post(
        [FromForm] ResumeUploadForm form,
        CancellationToken cancellationToken)
    {
        var suppliedFields = await Request.ReadFormAsync(cancellationToken);
        if (suppliedFields.Keys.Any(RejectedVersionFields.Contains))
        {
            throw new ValidationException(
                "Resume version fields are server-generated.",
                new Dictionary<string, string[]>
                {
                    ["version"] = ["version, versionYear, and versionSequence must not be supplied."],
                });
        }

        await using var stream = form.File.OpenReadStream();
        var result = await resumeService.UploadAsync(
            new ResumeUploadRequest(
                stream,
                form.File.FileName,
                form.File.ContentType,
                form.File.Length,
                form.Language,
                form.DescriptionEn,
                form.DescriptionVi,
                form.IsPublished,
                form.IsActive),
            cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse.Success(result, "Resume uploaded."));
    }

    [HttpPatch("{id:guid}/current")]
    [ProducesResponseType<ApiResponse<ResumeAdminResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ResumeAdminResponse>>> SetCurrent(
        Guid id,
        [FromBody] ResumeCurrentRequest request,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(
            await resumeService.SetCurrentAsync(id, request, cancellationToken),
            "Current Resume updated."));

    [HttpPatch("{id:guid}/publish")]
    [ProducesResponseType<ApiResponse<ResumeAdminResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ResumeAdminResponse>>> SetPublished(
        Guid id,
        [FromBody] ResumePublishRequest request,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(
            await resumeService.SetPublishedAsync(id, request, cancellationToken),
            "Resume publication updated."));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await resumeService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

public sealed class ResumeUploadForm
{
    public required IFormFile File { get; init; }
    public required string Language { get; init; }
    public string? DescriptionEn { get; init; }
    public string? DescriptionVi { get; init; }
    public bool IsPublished { get; init; }
    public bool IsActive { get; init; }
}
