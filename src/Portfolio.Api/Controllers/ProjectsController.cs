using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Authorization;
using Portfolio.Api.Models;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Projects;

namespace Portfolio.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/portfolio/{slug}/projects")]
public sealed class ProjectsController(IProjectService projectService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PublicProjectListItem>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublicProjectListItem>>>> Get(
        string slug, [FromQuery] string? locale, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(await projectService.GetPublicListAsync(slug, locale, cancellationToken)));

    [HttpGet("{projectSlug}")]
    [ProducesResponseType<ApiResponse<PublicProjectDetail>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PublicProjectDetail>>> GetBySlug(
        string slug, string projectSlug, [FromQuery] string? locale, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(await projectService.GetPublicDetailAsync(
            slug, projectSlug, locale, cancellationToken)));
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/admin/projects")]
public sealed class AdminProjectsController(IProjectService projectService) : ControllerBase
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ProjectAdminResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProjectAdminResponse>>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] ProjectKind? kind = null,
        [FromQuery] bool? isPublished = null,
        CancellationToken cancellationToken = default)
    {
        var result = await projectService.GetProjectsAsync(
            new ProjectAdminQuery(page, pageSize, search, kind, isPublished), cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<ProjectAdminResponse>>(
            result.Items,
            "Success",
            new PaginationMeta(result.Page, result.PageSize, result.Total, result.TotalPages)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<ProjectAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ProjectAdminResponse>>> GetById(
        Guid id, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(await projectService.GetProjectAsync(id, cancellationToken)));

    [HttpPost]
    [ProducesResponseType<ApiResponse<ProjectAdminResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ProjectAdminResponse>>> Post(
        [FromBody] ProjectWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await projectService.CreateProjectAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse.Success(result, "Project created."));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<ProjectAdminResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProjectAdminResponse>>> Put(
        Guid id, [FromBody] ProjectWriteRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(await projectService.UpdateProjectAsync(id, request, cancellationToken), "Project updated."));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await projectService.DeleteProjectAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id:guid}/publish")]
    [ProducesResponseType<ApiResponse<ProjectAdminResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ProjectAdminResponse>>> Publish(
        Guid id, [FromBody] ProjectPublishRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(await projectService.SetPublishedAsync(id, request, cancellationToken), "Project publication updated."));

    [HttpPatch("reorder")]
    [ProducesResponseType<ApiResponse<ProjectReorderRequest>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ProjectReorderRequest>>> Reorder(
        [FromBody] ProjectReorderRequest request, CancellationToken cancellationToken)
    {
        await projectService.ReorderProjectsAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(request, "Projects reordered."));
    }

    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ProjectImageMetadataResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProjectImageMetadataResponse>>>> UploadImages(
        Guid id,
        [FromForm] List<IFormFile> files,
        [FromForm] string metadata,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectGalleryMetadata> parsedMetadata;
        try
        {
            parsedMetadata = JsonSerializer.Deserialize<List<ProjectGalleryMetadata>>(
                metadata,
                WebJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            throw new ValidationException(
                "Project gallery validation failed.",
                new Dictionary<string, string[]> { ["metadata"] = ["Metadata must be valid JSON."] });
        }

        var streams = new List<Stream>(files.Count);
        try
        {
            var uploads = new List<ProjectGalleryUpload>(files.Count);
            for (var index = 0; index < files.Count; index++)
            {
                var stream = files[index].OpenReadStream();
                streams.Add(stream);
                uploads.Add(new ProjectGalleryUpload(
                    index, stream, files[index].FileName, files[index].ContentType, files[index].Length));
            }
            var result = await projectService.UploadGalleryAsync(id, uploads, parsedMetadata, cancellationToken);
            return Ok(ApiResponse.Success(result, "Project gallery uploaded."));
        }
        finally
        {
            foreach (var stream in streams) await stream.DisposeAsync();
        }
    }
}
