using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Contacts;

public sealed record ContactCreateRequest(
    [Required, StringLength(150, MinimumLength = 1)] string SenderName,
    [Required, EmailAddress, StringLength(320)] string SenderEmail,
    [Required, StringLength(200, MinimumLength = 1)] string Subject,
    [Required, StringLength(5000, MinimumLength = 1)] string Message,
    [StringLength(200)] string? Website);

public sealed record ContactSubmissionMetadata(string? IpHash, string? UserAgent);

public sealed record ContactReceiptResponse(Guid Id, DateTimeOffset ReceivedAt);

public sealed record ContactStatusRequest([Required] ContactStatus? Status);

public sealed record ContactAdminQuery(
    int Page,
    int PageSize,
    ContactStatus? Status,
    string? Search);

public sealed record ContactAdminResponse(
    Guid Id,
    string SenderName,
    string SenderEmail,
    string Subject,
    string Message,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ContactStatus>))]
    ContactStatus Status,
    bool IsSpam,
    string? IpHash,
    string? UserAgent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record ContactAdminPage(
    IReadOnlyList<ContactAdminResponse> Items,
    long Total,
    int Page,
    int PageSize)
{
    public int TotalPages => Total == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

public sealed record ContactMessagePage(
    IReadOnlyList<ContactMessage> Items,
    long Total,
    int Page,
    int PageSize);
