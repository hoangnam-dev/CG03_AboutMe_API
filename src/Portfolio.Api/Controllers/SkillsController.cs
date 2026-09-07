using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Authorization;
using Portfolio.Api.Models;
using Portfolio.Application.Skills;

namespace Portfolio.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/portfolio/{slug}/skills")]
public sealed class SkillsController(ISkillService skillService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PublicSkillCategoryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublicSkillCategoryResponse>>>> Get(
        string slug, [FromQuery] string? locale, CancellationToken cancellationToken)
    {
        var result = await skillService.GetPublicAsync(slug, locale, cancellationToken);
        return Ok(ApiResponse.Success(result));
    }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/admin/skill-categories")]
public sealed class AdminSkillCategoriesController(ISkillService skillService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SkillCategoryAdminResponse>>>> Get(
        CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(await skillService.GetCategoriesAsync(cancellationToken)));

    [HttpPost]
    [ProducesResponseType<ApiResponse<SkillCategoryAdminResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<SkillCategoryAdminResponse>>> Post(
        [FromBody] SkillCategoryWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await skillService.CreateCategoryAsync(request, cancellationToken);
        return Created($"/api/v1/admin/skill-categories/{result.Id:D}",
            ApiResponse.Success(result, "Skill category created."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SkillCategoryAdminResponse>>> Put(
        Guid id, [FromBody] SkillCategoryWriteRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(
            await skillService.UpdateCategoryAsync(id, request, cancellationToken),
            "Skill category updated."));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await skillService.DeleteCategoryAsync(id, cancellationToken);
        return NoContent();
    }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/admin/skills")]
public sealed class AdminSkillsController(ISkillService skillService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SkillAdminResponse>>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] bool? isPublished = null,
        CancellationToken cancellationToken = default)
    {
        var result = await skillService.GetSkillsAsync(
            new SkillAdminQuery(page, pageSize, search, categoryId, isPublished), cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<SkillAdminResponse>>(
            result.Items,
            "Success",
            new PaginationMeta(result.Page, result.PageSize, result.Total, result.TotalPages)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SkillAdminResponse>>> GetById(
        Guid id, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(await skillService.GetSkillAsync(id, cancellationToken)));

    [HttpPost]
    [ProducesResponseType<ApiResponse<SkillAdminResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<SkillAdminResponse>>> Post(
        [FromBody] SkillWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await skillService.CreateSkillAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id },
            ApiResponse.Success(result, "Skill created."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SkillAdminResponse>>> Put(
        Guid id, [FromBody] SkillWriteRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(
            await skillService.UpdateSkillAsync(id, request, cancellationToken),
            "Skill updated."));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await skillService.DeleteSkillAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("reorder")]
    public async Task<ActionResult<ApiResponse<SkillReorderRequest>>> Reorder(
        [FromQuery] Guid categoryId,
        [FromBody] SkillReorderRequest request,
        CancellationToken cancellationToken)
    {
        await skillService.ReorderSkillsAsync(categoryId, request, cancellationToken);
        return Ok(ApiResponse.Success(request, "Skills reordered."));
    }

    [HttpPost("{id:guid}/icon")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<SkillIconUploadResponse>>> UploadIcon(
        Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await skillService.ReplaceIconAsync(
            id,
            new SkillIconUpload(stream, file.FileName, file.ContentType, file.Length),
            cancellationToken);
        return Ok(ApiResponse.Success(result, "Skill icon replaced."));
    }
}
