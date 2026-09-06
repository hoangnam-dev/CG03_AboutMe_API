namespace Portfolio.Application.Common.Models;

public sealed class Profile
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool ShowEmail { get; set; }
    public bool ShowPhone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? HeroImageUrl { get; set; }
    public bool AvailableForWork { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<ProfileTranslation> Translations { get; } = [];
    public ICollection<SocialLink> SocialLinks { get; } = [];
    public About? About { get; set; }
}

public sealed class ProfileTranslation
{
    public Guid ProfileId { get; set; }
    public string LocaleCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ShortBio { get; set; }
    public string? Location { get; set; }
    public string? Availability { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Profile Profile { get; set; } = null!;
}

public sealed class SocialLink
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? IconName { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; }
    public Profile Profile { get; set; } = null!;
}

public sealed class About
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public decimal YearsOfExperience { get; set; }
    public int ProjectCount { get; set; }
    public int TechnologyCount { get; set; }
    public bool ShowYearsOfExperience { get; set; }
    public bool ShowProjectCount { get; set; }
    public bool ShowTechnologyCount { get; set; }
    public bool ShowContactSection { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Profile Profile { get; set; } = null!;
    public ICollection<AboutTranslation> Translations { get; } = [];
}

public sealed class AboutTranslation
{
    public Guid AboutId { get; set; }
    public string LocaleCode { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? CareerGoal { get; set; }
    public About About { get; set; } = null!;
}
