using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SIEMServer.Context;
using SIEMServer.Model;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Threading;

namespace SIEMServer.Service;

public sealed class BlacklistService
{
    private List<BlacklistedProcess> _cachedRules;
    private readonly ILogger<BlacklistService> _logger;
    private readonly IServiceProvider _serviceProvider;

    // Đối tượng lock để đảm bảo cache chỉ được tải một lần
    private static readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

    // 1. Hàm khởi tạo (Constructor)
    // Sạch sẽ và không block, chỉ tiêm (inject) dịch vụ
    public BlacklistService(IServiceProvider serviceProvider, ILogger<BlacklistService> logger)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _cachedRules = null; // Khởi tạo cache là null
        _logger.LogInformation("BlacklistService created (Cache is EMPTY).");
    }

    // 2. Hàm tải cache (nội bộ, private)
    private async Task LoadCacheAsync()
    {
        // Chờ để lấy khóa (lock)
        await _cacheLock.WaitAsync();
        try
        {
            // Kiểm tra lại sau khi lấy được khóa, 
            // phòng trường hợp một luồng (thread) khác đã tải nó trong lúc chờ
            if (_cachedRules != null)
            {
                return; // Cache đã được tải bởi luồng khác
            }

            _logger.LogInformation("Loading blacklist into cache for the first time...");
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SiemDbContext>();

                // Sử dụng .ToListAsync() (Bất đồng bộ)
                _cachedRules = await dbContext.BlacklistedProcesses
                    .AsNoTracking()
                    .ToListAsync();
            }

            _logger.LogInformation($"SUCCESSFULLY LOADED {_cachedRules.Count} rules into cache 🧠.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FATAL ERROR: Could not load Blacklist 📓!");
            _cachedRules = new List<BlacklistedProcess>(); // Khởi tạo danh sách rỗng nếu lỗi
        }
        finally
        {
            // Luôn giải phóng khóa
            _cacheLock.Release();
        }
    }

    // 3. Hàm công khai (public) mà API Controller sẽ gọi
    //    Hàm này sẽ tải (load) cache ở lần gọi đầu tiên
    public async Task<List<BlacklistedProcess>> GetRulesAsync()
    {
        // Kiểm tra xem cache đã được tải chưa
        if (_cachedRules == null)
        {
            // Nếu chưa, tải nó (một cách an toàn)
            await LoadCacheAsync();
        }

        return _cachedRules;
    }
    
}