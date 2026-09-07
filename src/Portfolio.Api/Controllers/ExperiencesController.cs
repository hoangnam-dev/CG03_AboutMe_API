using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Authorization;
using Portfolio.Api.Models;
using Portfolio.Application.Experiences;

namespace Portfolio.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/portfolio/{slug}/experiences")]
public sealed class ExperiencesController(IExperienceService experienceService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<PublicExperienceResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublicExperienceResponse>>>> Get(
        string slug, [FromQuery] string? locale, CancellationToken cancellationToken)
    {
        var result = await experienceService.GetPublicAsync(slug, locale, cancellationToken);
        return Ok(ApiResponse.Success(result));
    }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/admin/experiences")]
public sealed class AdminExperiencesController(IExperienceService experienceService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ExperienceAdminResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ExperienceAdminResponse>>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isPublished = null,
        CancellationToken cancellationToken = default)
    {
        var result = await experienceService.GetExperiencesAsync(
            new ExperienceAdminQuery(page, pageSize, search, isPublished), cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<ExperienceAdminResponse>>(
            result.Items,
            "Success",
            new PaginationMeta(result.Page, result.PageSize, result.Total, result.TotalPages)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<ExperienceAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ExperienceAdminResponse>>> GetById(
        Guid id, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(await experienceService.GetExperienceAsync(id, cancellationToken)));

    [HttpPost]
    [ProducesResponseType<ApiResponse<ExperienceAdminResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ExperienceAdminResponse>>> Post(
        [FromBody] ExperienceWriteRequest request, CancellationToken cancellationToken)
    {
        var result = await experienceService.CreateExperienceAsync(request, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse.Success(result, "Work experience created."));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<ExperienceAdminResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ExperienceAdminResponse>>> Put(
        Guid id,
        [FromBody] ExperienceWriteRequest request,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(
            await experienceService.UpdateExperienceAsync(id, request, cancellationToken),
            "Work experience updated."));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await experienceService.DeleteExperienceAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("reorder")]
    [ProducesResponseType<ApiResponse<ExperienceReorderRequest>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApiResponse<ExperienceReorderRequest>>> Reorder(
        [FromBody] ExperienceReorderRequest request,
        CancellationToken cancellationToken)
    {
        await experienceService.ReorderExperiencesAsync(request, cancellationToken);
        return Ok(ApiResponse.Success(request, "Work experiences reordered."));
    }
}
