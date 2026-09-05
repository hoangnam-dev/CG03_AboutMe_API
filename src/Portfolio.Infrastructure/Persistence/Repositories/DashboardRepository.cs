using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Dashboard;

namespace Portfolio.Infrastructure.Persistence.Repositories;

public sealed class DashboardRepository(PortfolioDbContext context) : IDashboardRepository
{
    public async Task<DashboardStats> GetStatsAsync(CancellationToken cancellationToken)
    {
        var projects = await context.Projects.CountAsync(cancellationToken);
        var certificates = await context.Certificates.CountAsync(cancellationToken);
        var resumes = await context.Resumes.CountAsync(cancellationToken);
        var unreadContacts = await context.ContactMessages.CountAsync(
            message => message.Status == ContactStatus.New,
            cancellationToken);

        return new DashboardStats(projects, certificates, resumes, unreadContacts);
    }
}
