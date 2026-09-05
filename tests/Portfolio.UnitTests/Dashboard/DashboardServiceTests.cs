using Portfolio.Application.Dashboard;
using Xunit;

namespace Portfolio.UnitTests.Dashboard;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task GetStatsReturnsRepositoryCounts()
    {
        var expected = new DashboardStats(4, 3, 2, 1);
        var service = new DashboardService(new StubDashboardRepository(expected));

        var result = await service.GetStatsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
    }

    private sealed class StubDashboardRepository(DashboardStats stats)
        : IDashboardRepository
    {
        public Task<DashboardStats> GetStatsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(stats);
    }
}
