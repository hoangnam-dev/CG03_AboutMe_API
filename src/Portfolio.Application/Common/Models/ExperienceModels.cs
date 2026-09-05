namespace Portfolio.Application.Common.Models;

public enum ExperienceHighlightType
{
    Responsibility,
    Achievement,
}

public sealed class WorkExperience
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? EmploymentType { get; set; }
    public string? CompanyUrl { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<ExperienceTranslation> Translations { get; } = [];
    public ICollection<ExperienceHighlight> Highlights { get; } = [];
    public ICollection<ExperienceTechnology> Technologies { get; } = [];
}

public sealed class ExperienceTranslation
{
    public Guid ExperienceId { get; set; }
    public string LocaleCode { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Description { get; set; }
    public WorkExperience Experience { get; set; } = null!;
}

public sealed class ExperienceHighlight
{
    public Guid Id { get; set; }
    public Guid ExperienceId { get; set; }
    public string LocaleCode { get; set; } = string.Empty;
    public ExperienceHighlightType HighlightType { get; set; }
    public string Content { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public WorkExperience Experience { get; set; } = null!;
}

public sealed class ExperienceTechnology
{
    public Guid ExperienceId { get; set; }
    public Guid TechnologyId { get; set; }
    public int DisplayOrder { get; set; }
    public WorkExperience Experience { get; set; } = null!;
    public Technology Technology { get; set; } = null!;
}
