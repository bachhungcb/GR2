using Microsoft.AspNetCore.Mvc;
using SIEMServer.Service;

namespace SIEMServer.Controllers;

[ApiController]
[Route("api/blacklist")]
public class BlacklistController : ControllerBase
{
    private readonly BlacklistService _blacklistService;

    public BlacklistController(BlacklistService blacklistService)
    {
        _blacklistService = blacklistService;
    }

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules()
    {
        // Sử dụng phiên bản GetRulesAsync chúng ta đã thảo luận
        // (phiên bản tải cache an toàn, bất đồng bộ)
        var rules = await _blacklistService.GetRulesAsync();
        return Ok(rules);
    }
}