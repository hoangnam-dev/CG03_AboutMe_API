namespace Portfolio.Application.Resumes;

public static class ResumeVersionFormatter
{
    public static string Format(short year, int sequence) => $"v{year}_{sequence:00}";
}
