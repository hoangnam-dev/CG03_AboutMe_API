using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portfolio.Api.Authorization;
using Portfolio.Api.Models;
using Portfolio.Application.Dashboard;

namespace Portfolio.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/admin/dashboard")]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<DashboardStats>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<DashboardStats>>> Get(
        CancellationToken cancellationToken)
    {
        var result = await dashboardService.GetStatsAsync(cancellationToken);
        return Ok(ApiResponse.Success(result));
    }
}
