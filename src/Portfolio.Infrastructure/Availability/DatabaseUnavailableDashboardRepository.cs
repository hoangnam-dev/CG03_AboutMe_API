using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Dashboard;

namespace Portfolio.Infrastructure.Availability;

internal sealed class DatabaseUnavailableDashboardRepository : IDashboardRepository
{
    public Task<DashboardStats> GetStatsAsync(CancellationToken cancellationToken) =>
        throw new ServiceUnavailableException(
            "Dashboard data is unavailable because PostgreSQL is not configured.");
}
