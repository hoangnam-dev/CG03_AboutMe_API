using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Authorization;
using Portfolio.Api.Models;
using Portfolio.Application.Certificates;

namespace Portfolio.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/portfolio/{slug}/certificates")]
public sealed class CertificatesController(ICertificateService certificateService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<CertificatePublicResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CertificatePublicResponse>>>> Get(
        string slug,
        [FromQuery] string? locale,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(await certificateService.GetPublicAsync(slug, locale, cancellationToken)));
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/admin/certificates")]
public sealed class AdminCertificatesController(ICertificateService certificateService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<CertificateAdminResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CertificateAdminResponse>>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isPublished = null,
        CancellationToken cancellationToken = default)
    {
        var result = await certificateService.GetCertificatesAsync(
            new CertificateAdminQuery(page, pageSize, search, isPublished), cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<CertificateAdminResponse>>(
            result.Items,
            "Success",
            new PaginationMeta(result.Page, result.PageSize, result.Total, result.TotalPages)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<CertificateAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CertificateAdminResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(await certificateService.GetCertificateAsync(id, cancellationToken)));

    [HttpPost]
    [ProducesResponseType<ApiResponse<CertificateAdminResponse>>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<CertificateAdminResponse>>> Post(
        [FromBody] CertificateWriteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await certificateService.CreateCertificateAsync(request, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse.Success(result, "Certificate created."));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<CertificateAdminResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CertificateAdminResponse>>> Put(
        Guid id,
        [FromBody] CertificateWriteRequest request,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(
            await certificateService.UpdateCertificateAsync(id, request, cancellationToken),
            "Certificate updated."));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await certificateService.DeleteCertificateAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/file")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ApiResponse<CertificateEvidenceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<ApiResponse<CertificateEvidenceResponse>>> UploadFile(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await certificateService.UploadEvidenceAsync(
            id,
            new CertificateEvidenceUpload(
                stream,
                file.FileName,
                file.ContentType,
                file.Length),
            cancellationToken);
        return Ok(ApiResponse.Success(result, "Certificate evidence uploaded."));
    }
}
