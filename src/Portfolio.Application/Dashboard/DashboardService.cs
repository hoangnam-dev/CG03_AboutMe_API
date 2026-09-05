namespace Portfolio.Application.Dashboard;

public sealed class DashboardService(IDashboardRepository repository) : IDashboardService
{
    public Task<DashboardStats> GetStatsAsync(CancellationToken cancellationToken) =>
        repository.GetStatsAsync(cancellationToken);
}
