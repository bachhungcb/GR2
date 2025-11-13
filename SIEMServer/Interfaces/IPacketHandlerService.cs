namespace SIEMServer.Interfaces;
using System.Net.Sockets;

public interface IPacketHandlerService
{
    /// <summary>
    /// Xử lý một gói tin (packet) dữ liệu đo đạc (telemetry) thô (raw) duy nhất từ một agent (máy khách).
    /// </summary>
    /// <param name="jsonBuffer">Gói tin (packet) JSON thô (raw).</param>
    /// <param name="remoteIpAddress">Địa chỉ IP của agent (máy khách) đã gửi nó.</param>
    Task ProcessPacketAsync(byte[] jsonBuffer, string remoteIpAddress, NetworkStream replyStream);
}