namespace Portfolio.Application.Common.Models;

public sealed class ResumeVersionCounter
{
    public string LanguageCode { get; set; } = string.Empty;
    public short VersionYear { get; set; }
    public int LastSequence { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<ResumeFile> Resumes { get; } = [];
}

public sealed class ResumeFile
{
    public Guid Id { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public short VersionYear { get; set; }
    public int VersionSequence { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ResumeVersionCounter VersionCounter { get; set; } = null!;
    public ICollection<ResumeTranslation> Translations { get; } = [];
}

public sealed class ResumeTranslation
{
    public Guid ResumeId { get; set; }
    public string LocaleCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ResumeFile Resume { get; set; } = null!;
}
