namespace Portfolio.Application.Dashboard;

public interface IDashboardRepository
{
    Task<DashboardStats> GetStatsAsync(CancellationToken cancellationToken);
}
