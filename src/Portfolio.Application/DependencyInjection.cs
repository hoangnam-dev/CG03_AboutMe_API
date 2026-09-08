using Microsoft.Extensions.DependencyInjection;
using Portfolio.Application.About;
using Portfolio.Application.Authentication;
using Portfolio.Application.Certificates;
using Portfolio.Application.Common.Storage;
using Portfolio.Application.Dashboard;
using Portfolio.Application.Experiences;
using Portfolio.Application.Profiles;
using Portfolio.Application.Projects;
using Portfolio.Application.Resumes;
using Portfolio.Application.Skills;

namespace Portfolio.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICertificateService, CertificateService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IAboutService, AboutService>();
        services.AddScoped<IExperienceService, ExperienceService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IResumeService, ResumeService>();
        services.AddScoped<ISkillService, SkillService>();
        services.AddScoped<StorageReplacement>();
        return services;
    }
}
