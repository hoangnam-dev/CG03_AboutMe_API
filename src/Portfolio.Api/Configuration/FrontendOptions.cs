namespace Portfolio.Api.Configuration;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string Origin { get; set; } = string.Empty;
}
