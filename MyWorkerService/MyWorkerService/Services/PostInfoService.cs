using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MyWorkerService.Services
{
    public sealed class PostInfoService
    {
        // Khởi tạo là null. Chúng ta sẽ tạo chúng trong hàm.
        private TcpClient tcpClient = null;
        private NetworkStream stream = null;

        private readonly HandleCommandService _commandHandler;
        private readonly ILogger<PostInfoService> _logger;

        public PostInfoService(
            HandleCommandService commandHandler,
            ILogger<PostInfoService> logger)
        {
            _commandHandler = commandHandler;
            _logger = logger;
        }


        public async Task PostInformation(byte[] sendByte)
        {
            try
            {
                // 1. Establish connections
                IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
                IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, 8888);

                if (!tcpClient.Connected)
                {
                    await CleanUpConnection(); // Dùng hàm helper an toàn
                    tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(ipEndPoint);
                    stream = tcpClient.GetStream();
                }

                // 2. Prepare packet
                int packetLength = sendByte.Length;
                int networkOrderLength = IPAddress.HostToNetworkOrder(packetLength);
                byte[] header = BitConverter.GetBytes(networkOrderLength);
                
                // 3. Send two packets 
                await stream.WriteAsync(header, 0 ,header.Length);
                await stream.WriteAsync(sendByte, 0, sendByte.Length);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, "[PostInformation] Unhandled Exception. Forcing reconnect.");
                await CleanUpConnection();
            }
        }

        private async Task CleanUpConnection()
        {
            if (stream != null)
            {
                await stream.DisposeAsync();
                stream = null;
            }

            if (tcpClient != null)
            {
                tcpClient.Dispose();
                tcpClient = null;
            }
        }
    }
}