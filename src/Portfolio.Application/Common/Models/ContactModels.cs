using System.Text.Json;
using System.Text.Json.Serialization;

namespace Portfolio.Application.Common.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ContactStatus>))]
public enum ContactStatus
{
    New,
    Read,
    Archived,
}

public sealed class ContactMessage
{
    public Guid Id { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ContactStatus Status { get; set; }
    public bool IsSpam { get; set; }
    public string? IpHash { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class SiteSetting
{
    public string Key { get; set; } = string.Empty;
    public JsonDocument Value { get; set; } = null!;
    public string? Description { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
