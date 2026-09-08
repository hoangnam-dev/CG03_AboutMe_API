using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Portfolio.Api.Authorization;
using Portfolio.Api.Configuration;
using Portfolio.Api.Models;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Contacts;

namespace Portfolio.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/contact")]
public sealed class ContactsController(IContactService contactService) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("ContactSubmission")]
    [RequestSizeLimit(ContactRequestLimits.MaxBodySize)]
    [ProducesResponseType<ApiResponse<ContactReceiptResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<ContactReceiptResponse>>> Post(
        ContactCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await contactService.CreateContactAsync(
            request,
            new ContactSubmissionMetadata(
                HashRemoteIp(HttpContext.Connection.RemoteIpAddress),
                Request.Headers.UserAgent.ToString()),
            cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse.Success(result, "Message received."));
    }

    private static string? HashRemoteIp(System.Net.IPAddress? remoteIp)
    {
        if (remoteIp is null)
            return null;

        var normalized = remoteIp.IsIPv4MappedToIPv6
            ? remoteIp.MapToIPv4().ToString()
            : remoteIp.ToString();
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(digest);
    }
}

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/admin/contacts")]
public sealed class AdminContactsController(IContactService contactService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ContactAdminResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ContactAdminResponse>>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ContactStatus? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await contactService.GetContactsAsync(
            new ContactAdminQuery(page, pageSize, status, search),
            cancellationToken);
        return Ok(new ApiResponse<IReadOnlyList<ContactAdminResponse>>(
            result.Items,
            "Success",
            new PaginationMeta(result.Page, result.PageSize, result.Total, result.TotalPages)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<ContactAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<ContactAdminResponse>>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(await contactService.GetContactAsync(id, cancellationToken)));

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<ApiResponse<ContactAdminResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<ContactAdminResponse>>> PatchStatus(
        Guid id,
        ContactStatusRequest request,
        CancellationToken cancellationToken) =>
        Ok(ApiResponse.Success(
            await contactService.UpdateStatusAsync(id, request, cancellationToken),
            "Contact status updated."));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await contactService.DeleteContactAsync(id, cancellationToken);
        return NoContent();
    }
}
