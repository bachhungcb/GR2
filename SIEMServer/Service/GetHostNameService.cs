using System.Net;

namespace SIEMServer.Service;

public class GetHostNameService
{
    /// <summary>
    /// Hàm helper MỚI: Chỉ tra cứu và trả về Hostname
    /// </summary>
    public async Task<string> ResolveHostnameSimpleAsync(string ipAddress)
    {
        try
        {
            IPAddress ip = IPAddress.Parse(ipAddress);

            // Bỏ qua tra cứu các IP nội bộ (private) hoặc loopback
            if (IPAddress.IsLoopback(ip) || IsPrivateIP(ip))
            {
                return ipAddress; // Trả về chính IP đó
            }
        
            // Thực hiện tra cứu DNS ngược
            IPHostEntry hostEntry = await Dns.GetHostEntryAsync(ip);
            return hostEntry.HostName; // Chỉ trả về tên (name)
        }
        catch (Exception)
        {
            // Nếu tra cứu thất bại, trả về IP gốc
            return ipAddress; 
        }
    }
    
    public async Task<string> GetHostnameAsync(Telemetry.Telemetry.TCPJsonElement connection)
    {
        try
        {
            // Lấy IP từ chuỗi string
            IPAddress ip = IPAddress.Parse(connection.RemoteEndPointAddr);

            // Bỏ qua các IP nội bộ (private) hoặc loopback
            if (IPAddress.IsLoopback(ip) || IsPrivateIP(ip))
            {
                // Giữ nguyên, không tra cứu (chỉ thêm State)
                return $"{connection.ToString()} [{connection.State}]";
            }

            // Thực hiện tra cứu DNS ngược
            IPHostEntry hostEntry = await Dns.GetHostEntryAsync(ip);

            // Trả về dòng mới: IP -> Hostname (Trạng thái)
            // (Chúng ta dùng connection.ToString() vì nó đã có định dạng IP -> IP)
            return $"{connection.ToString()} ({hostEntry.HostName}) [{connection.State}]";
        }
        catch (System.Net.Sockets.SocketException)
        {
            // LỖI: Không tìm thấy hostname (rất phổ biến)
            // Chỉ cần trả về chuỗi gốc (thêm State)
            return $"{connection.ToString()} [{connection.State}]";
        }
        catch (Exception)
        {
            // Bắt các lỗi khác (ví dụ: IPAddress.Parse thất bại)
            return $"{connection.ToString()} [Invalid IP]";
        }
    }

    // Hàm tiện ích để kiểm tra IP Private (không cần tra cứu DNS)
    private bool IsPrivateIP(IPAddress ip)
    {
        // Chuyển sang IPv4 nếu là IPv6 mapped
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        var bytes = ip.GetAddressBytes();

        // Kiểm tra IPv4
        switch (bytes[0])
        {
            case 10: // 10.x.x.x
                return true;
            case 172: // 172.16.x.x - 172.31.x.x
                return (bytes[1] >= 16 && bytes[1] <= 31);
            case 192: // 192.168.x.x
                return (bytes[1] == 168);
            default:
                return false;
        }
    }
}