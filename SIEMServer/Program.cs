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

Console.WriteLine("Starting SIEM server....");
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

#region Services

// 1. Đăng ký (Register) DBContext (Code của bạn đã đúng)
builder.Services.AddDbContext<SiemDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOption => { sqlOption.CommandTimeout(30); }
    )
);

// 2. Đăng ký (Register) các DỊCH VỤ (SERVICES) "SCOPED" (CÓ PHẠM VI)
// (Chúng sẽ được tạo MỚI cho mỗi gói tin (packet))
builder.Services.AddScoped<GetHostNameService>();
builder.Services.AddScoped<IPacketHandlerService, PacketHandlerService>();

// 3. Đăng ký (Register) các DỊCH VỤ (SERVICES) "SINGLETON" (CHẠY MÃI MÃI)
// builder.Services.AddHostedService<DnsResolutionService>();
builder.Services.AddSingleton<BlacklistService>();
builder.Services.AddSingleton<PacketChannelService>();
builder.Services.AddHostedService<PacketProcessingService>();


//Singleton
builder.Services.AddSingleton<TCPServer>();
builder.Services.AddHostedService<TCPServerService>();

#endregion

// Xây dựng (Build) và Chạy (Run) (Code của bạn đã đúng)
IHost host = builder.Build();
host.Run();