namespace Portfolio.Application.Common.Models;

public enum ProjectKind
{
    Personal,
    Professional,
}

public enum ProjectDisclosureLevel
{
    Full,
    Limited,
}

public sealed class Project
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string InternalName { get; set; } = string.Empty;
    public ProjectKind Kind { get; set; }
    public ProjectDisclosureLevel DisclosureLevel { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? RepositoryUrl { get; set; }
    public string? DemoUrl { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsFeatured { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<ProjectTranslation> Translations { get; } = [];
    public ICollection<ProjectTechnology> Technologies { get; } = [];
    public ICollection<ProjectHighlight> Highlights { get; } = [];
    public ICollection<ProjectImage> Images { get; } = [];
}

public sealed class ProjectTranslation
{
    public Guid ProjectId { get; set; }
    public string LocaleCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortDescription { get; set; }
    public string? ClientContext { get; set; }
    public string? Role { get; set; }
    public string? Problem { get; set; }
    public string? Solution { get; set; }
    public string? Result { get; set; }
    public Project Project { get; set; } = null!;
}

public sealed class ProjectTechnology
{
    public Guid ProjectId { get; set; }
    public Guid TechnologyId { get; set; }
    public int DisplayOrder { get; set; }
    public Project Project { get; set; } = null!;
    public Technology Technology { get; set; } = null!;
}

public sealed class ProjectHighlight
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string LocaleCode { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public Project Project { get; set; } = null!;
}

public sealed class ProjectImage
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Project Project { get; set; } = null!;
    public ICollection<ProjectImageTranslation> Translations { get; } = [];
}

public sealed class ProjectImageTranslation
{
    public Guid ProjectImageId { get; set; }
    public string LocaleCode { get; set; } = string.Empty;
    public string AltText { get; set; } = string.Empty;
    public ProjectImage ProjectImage { get; set; } = null!;
}
