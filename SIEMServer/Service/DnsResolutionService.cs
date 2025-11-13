using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIEMServer.Context;
using SIEMServer.Service;

namespace MyWorkerService.Services;

public sealed class DnsResolutionService : BackgroundService
{
    private readonly ILogger<DnsResolutionService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    // 1. Chúng ta "tiêm" (inject) "Nhà máy" (Factory) 🏭, 
    //    vì Dịch vụ (Service) này là Singleton 
    //    nhưng nó cần các dịch vụ (services) Scoped 📦 (như DbContext)
    public DnsResolutionService(
        ILogger<DnsResolutionService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Service DNS Lookup " +
                               "(Cold path) is running.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 2.Create a new scope for this run 
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<SiemDbContext>();
                    var hostNameService = scope.ServiceProvider.GetRequiredService<GetHostNameService>();

                    _logger.LogInformation("Scanning DB for unresolved IP address");
                    // 3. TÌM (FIND) VIỆC ĐỂ LÀM
                    // Lấy (Get) 100 mục (items) "chưa được xử lý" (chưa có Tên (Name))

                    var entriesToFix = await dbContext.ConnectionEntries
                        .Where(c => string.IsNullOrEmpty(c.RemoteEndPointName))
                        .Take(100) // only take 100 entries each time for preventing overload
                        .ToListAsync(stoppingToken);

                    if (entriesToFix.Any())
                    {
                        _logger.LogInformation($"Found {entriesToFix.Count} entries waiting to be resolved");

                        // 4. THỰC HIỆN (PERFORM) TRA CỨU (LOOKUP) "CHẬM" (SLOW) 🐢
                        foreach (var entry in entriesToFix)
                        {
                            // Chúng ta `await` (chờ) từng cái một, 
                            // vì đây là luồng (thread) nền (background), 
                            // chúng ta không cần vội
                            entry.RemoteEndPointName =
                                await hostNameService.ResolveHostnameSimpleAsync(entry.RemoteEndPointAddr);

                            // (Tương tự cho LocalName nếu bạn muốn)
                            entry.LocalEndPointName =
                                await hostNameService.ResolveHostnameSimpleAsync(entry.LocalEndPointAddr);
                        }

                        // 5. LƯU (SAVE) KẾT QUẢ
                        await dbContext.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation($"Updated {entriesToFix.Count} records.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Lookup DNS Service");
            }

            // 6. "NGỦ" (SLEEP) 😴
            // Chờ 5 phút trước khi "quét" (scan) lại
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}