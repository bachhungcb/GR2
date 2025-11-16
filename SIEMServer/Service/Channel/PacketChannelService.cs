namespace SIEMServer.Service.Channel;

using System.Threading.Channels;

/// <summary>
/// DTO (Đối tượng Truyền dữ liệu)
/// chứa gói tin (packet) thô (raw) và IP (Địa chỉ) của người gửi (sender).
/// </summary>
public class RawPacket
{
    public byte[] JsonBuffer { get; set; }
    public string AgentIp { get; set; }
}

/// <summary>
/// Dịch vụ (Service) Singleton 
/// giữ (hold) một hàng đợi (queue) trong bộ nhớ (in-memory)
/// để tách (decouple) việc "Đọc" (Reading) khỏi việc "Xử lý" (Processing) 🐢.
/// </summary>
public class PacketChannelService
{
    private readonly Channel<RawPacket> _channel;

    public PacketChannelService()
    {
        var options = new BoundedChannelOptions(1000)
        {
            // Khi hàng đợi đầy, yêu cầu "Producer" (TCPServer) chờ
            FullMode = BoundedChannelFullMode.Wait
        };
        // Tạo (Create) một hàng đợi (queue) "không giới hạn" (unbounded) 
        // (có thể giữ (hold) vô số gói tin (packets) trong bộ nhớ (memory))
        _channel = Channel.CreateBounded<RawPacket>(options);
    }

    /// <summary>
    /// Được gọi (Called) bởi "Người phục vụ" (Waiter) ⚡️ (TCPServer) 
    /// để "Quẳng" (Put) 📥 một gói tin (packet) mới vào hàng đợi (queue).
    /// (Đây là một hành động (action) rất nhanh ⚡️)
    /// </summary>
    public async Task WriteAsync(
        RawPacket packet,
        CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(packet, ct);
    }

    /// <summary>
    /// Được gọi (Called) bởi "Đầu bếp" (Kitchen) (PacketProcessingService) 
    /// để "Chờ" (Wait)một gói tin (packet) mới.
    /// </summary>
    public async Task<RawPacket> ReadAsync(CancellationToken ct = default)
    {
        return await _channel.Reader.ReadAsync(ct);
    }

    /// <summary>
    /// Được gọi (Called) bởi "Đầu bếp" (Kitchen) để kiểm tra (check)
    /// xem có gói tin (packet) nào không.
    /// </summary>
    public bool TryRead(out RawPacket packet)
    {
        return _channel.Reader.TryRead(out packet);
    }

    /// <summary>
    /// Được gọi (Called) bởi "Đầu bếp" (Kitchen) ️ để "Chờ" (Wait)
    /// một cách hiệu quả.
    /// </summary>
    public Task WaitForReadAsync(CancellationToken ct = default)
    {
        return _channel.Reader.WaitToReadAsync(ct).AsTask();
    }
}