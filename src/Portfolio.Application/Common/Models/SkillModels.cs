namespace Portfolio.Application.Common.Models;

public enum SkillLevel
{
    Primary,
    Experienced,
    Familiar,
    Learning,
}

public enum SkillIconType
{
    Lucide,
    Image,
    Text,
}

public sealed class SkillCategory
{
    public Guid Id { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<SkillCategoryTranslation> Translations { get; } = [];
    public ICollection<Technology> Technologies { get; } = [];
}

public sealed class SkillCategoryTranslation
{
    public Guid CategoryId { get; set; }
    public string LocaleCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SkillCategory Category { get; set; } = null!;
}

public sealed class Technology
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public SkillLevel SkillLevel { get; set; }
    public decimal? YearsOfExperience { get; set; }
    public SkillIconType IconType { get; set; }
    public string IconValue { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public SkillCategory Category { get; set; } = null!;
    public ICollection<TechnologyTranslation> Translations { get; } = [];
    public ICollection<ExperienceTechnology> Experiences { get; } = [];
    public ICollection<ProjectTechnology> Projects { get; } = [];
    public ICollection<CertificateTechnology> Certificates { get; } = [];
}

public sealed class TechnologyTranslation
{
    public Guid TechnologyId { get; set; }
    public string LocaleCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Technology Technology { get; set; } = null!;
}
