using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyWorkerService.Command;

namespace MyWorkerService.Services;

public class DomainBlockerService : BackgroundService
{
    private readonly ILogger<DomainBlockerService> _logger;
    private readonly LocalBlacklistService _localBlacklistSerivce;
    private readonly HandleCommandService _handleCommandSerivce;

    // Cấu hình thời gian (bạn có thể thay đổi tùy ý)
    private readonly TimeSpan _longInterval = TimeSpan.FromMinutes(1); // Khi đã hoạt động ổn định
    private readonly TimeSpan _shortInterval = TimeSpan.FromSeconds(30); // Khi đang chờ dữ liệu ban đầu

    public DomainBlockerService(
        ILogger<DomainBlockerService> logger,
        LocalBlacklistService localBlackListService,
        HandleCommandService commandHandler)
    {
        _logger = logger;
        _localBlacklistSerivce = localBlackListService;
        _handleCommandSerivce = commandHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Domain Blocker Service started.");

        while (!cancellationToken.IsCancellationRequested)
        {
            bool hasWorkToDo = false;

            try
            {
                // 1. Lấy danh sách Domain cần chặn
                var domains = _localBlacklistSerivce.GetBlacklistedDomains();

                if (domains.Any())
                {
                    hasWorkToDo = true;
                    _logger.LogInformation($"[DNS SCAN] Resolving IPs for {domains.Count} domains...");
                    foreach (var domain in domains)
                    {
                        await ResolveAndBlock(domain);
                    }
                }
                else
                {
                    _logger.LogDebug("[DNS SCAN] No domains found in blacklist yet.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Domain Blocker loop");
            }

            if (hasWorkToDo)
            {
                await Task.Delay(_shortInterval, cancellationToken);
            }
            else
            {
                await Task.Delay(_longInterval, cancellationToken);   
            }
        }
    }

    private async Task ResolveAndBlock(string domain)
    {
        try
        {
            // [QUAN TRỌNG] Dùng DNS của hệ thống để "đào" (dig) ra IP
            // Hàm này trả về cả IPv4 và IPv6
            IPAddress[] ips = await Dns.GetHostAddressesAsync(domain);

            foreach (var ip in ips)
            {
                // Bỏ qua IPv6 nếu bạn chưa muốn xử lý (hoặc chặn cả hai)
                // Ở đây chặn hết
                string ipString = ip.ToString();

                _logger.LogInformation($"[DNS DETECT] Domain '{domain}' resolved to IP: {ipString}");

                // Gọi HandleCommandService để thêm Firewall Rule
                // (Service này đã có sẵn logic check trùng lặp, nên cứ gọi thoải mái)
                var command = new ServerCommand
                {
                    CommandType = "BLOCK_IP",
                    Target = ipString
                };
                _handleCommandSerivce.ExecuteCommand(command);
            }
        }
        catch (Exception ex)
        {
            // Domain không tồn tại hoặc lỗi DNS
            _logger.LogWarning($"Could not resolve domain '{domain}': {ex.Message}");
        }
    }
}