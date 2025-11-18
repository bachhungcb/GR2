using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIEMServer.Context;

namespace SIEMServer.Controllers;

[ApiController]
[Route("api/alerts")]
public class AlertsController : ControllerBase
{
    private readonly SiemDbContext _context;

    public AlertsController(SiemDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecentAlerts([FromQuery] int count = 50)
    {
        var alerts = await _context.Alerts
            .AsNoTracking()
            .Include(a => a.Agent) // Kèm thông tin Agent để lấy HostName
            .OrderByDescending(a => a.Timestamp) // Mới nhất lên đầu
            .Take(count)
            .Select(a => new
            {
                a.Id,
                AgentName = a.Agent.HostName,
                a.ProcessName,
                a.Pid,
                a.MatchedRule,
                a.Timestamp
            })
            .ToListAsync();

        return Ok(alerts);
    }
}