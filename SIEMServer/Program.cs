using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyWorkerService.Services;
using SIEMServer.Context;
using SIEMServer.Interfaces;
using SIEMServer.Service;
using SIEMServer.Service.Channel; // Thêm (Add) 'using' này
using SIEMServer.TCP;
using Serilog;

Console.WriteLine("Starting SIEM server....");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console() // Ghi log ra Console
    .WriteTo.File( // Ghi log ra file
        @"c:\temp\siemserver_log.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger(); // Logger tạm thời để bắt lỗi khởi động
try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);


    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(8889); // Cổng cho API
    });
    builder.Logging.ClearProviders();
    builder.Host.UseSerilog();
    #region Services

    builder.Services.AddControllers();

// 1. Đăng ký (Register) DBContext (Code của bạn đã đúng)
    builder.Services.AddDbContext<SiemDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOption => { sqlOption.CommandTimeout(30); }
        )
    );

// 2. Đăng ký (Register) các DỊCH VỤ (SERVICES) "SCOPED" (Giữ nguyên)
    builder.Services.AddScoped<GetHostNameService>();
    builder.Services.AddScoped<IPacketHandlerService, PacketHandlerService>();

// 3. Đăng ký (Register) các DỊCH VỤ (SERVICES) "SINGLETON" (Giữ nguyên)
    builder.Services.AddSingleton<BlacklistService>(); // API sẽ dùng cái này
    builder.Services.AddSingleton<PacketChannelService>();
    builder.Services.AddHostedService<PacketProcessingService>();

//Singleton
    builder.Services.AddSingleton<TCPServer>();
    builder.Services.AddHostedService<TCPServerService>(); // Dịch vụ chạy TCPServer

    #endregion

// Xây dựng (Build) và Chạy (Run)
    var app = builder.Build();

// [MỚI] Ánh xạ các API controllers
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush(); // [MỚI] Đảm bảo mọi log đều được ghi trước khi tắt
}