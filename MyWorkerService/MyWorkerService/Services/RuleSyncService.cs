using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyWorkerService.Services;

public class RuleSyncService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LocalBlacklistService _localBlacklistService;

    private readonly ILogger<RuleSyncService> _logger;

    // [SỬA] Đặt URL server của bạn ở đây
    private readonly string _serverApiUrl = "http://127.0.0.1:8889/api/blacklist/rules";

    public RuleSyncService(
        IHttpClientFactory httpClientFactory,
        LocalBlacklistService localBlacklistService,
        ILogger<RuleSyncService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _localBlacklistService = localBlacklistService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Syncing blacklist rules from server...");
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                var response = await client.GetAsync(_serverApiUrl, stoppingToken);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync(stoppingToken);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var newRules =
                        JsonSerializer.Deserialize<List<BlacklistedProcess>>(jsonString, options);

                    if (newRules != null)
                    {
                        _localBlacklistService.UpdateRules(newRules);
                    }
                }
                else
                {
                    _logger.LogWarning($"Failed to sync rules. Status code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while syncing rules.");
            }

            // Chờ 5 phút rồi đồng bộ lại
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}