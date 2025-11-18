using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIEMServer.Context;

namespace SIEMServer.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentsController : ControllerBase
{
    private readonly SiemDbContext _context;

    public AgentsController(SiemDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAgents()
    {
        // Định nghĩa ngưỡng thời gian (ví dụ: 2 phút trước)
        var threshold = DateTime.Now.AddMinutes(-2);

        var agents = await _context.Agents
            .AsNoTracking()
            .Select(a => new
            {
                a.Id,
                a.HostName,
                a.FirstSeen,
                a.LastSeen,
                // Tính toán trạng thái Online ngay trong câu truy vấn
                IsOnline = a.LastSeen >= threshold,
                // Đếm số lượng Alert của Agent này (tuỳ chọn)
                AlertCount = _context.Alerts.Count(al => al.AgentId == a.Id)
            })
            .OrderByDescending(a => a.LastSeen) // Agent mới nhất lên đầu
            .ToListAsync();

        return Ok(agents);
    }
}