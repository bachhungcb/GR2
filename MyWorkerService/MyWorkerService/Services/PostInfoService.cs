using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;

namespace MyWorkerService.Services
{
    public sealed class PostInfoService
    {
        private TcpClient tcpClient = new TcpClient();
        private NetworkStream stream;

        //---UPGRADE
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
                // 1. Establish connection
                IPAddress iPAddress = IPAddress.Parse("127.0.0.1");
                IPEndPoint iPEndPoint = new IPEndPoint(iPAddress, 8888);

                if (!tcpClient.Connected)
                {
                    // 2. Check connection stream
                    if (stream != null) await stream.DisposeAsync();
                    tcpClient.Dispose();
                    tcpClient = new TcpClient();

                    // 3. Create new conncetions    
                    await tcpClient.ConnectAsync(iPEndPoint);
                    stream = tcpClient.GetStream();
                }
                // 4. Prepare Packet 

                // 4.1. Get packet length
                int packetLength = sendByte.Length;

                // 4.2. Conver packet length to NetworkByteOrder
                int networkOrderLength = IPAddress.NetworkToHostOrder(packetLength);

                // 4.3. Convert int to 4-byte byte array
                byte[] header = BitConverter.GetBytes(networkOrderLength);

                // 5. Send two packets

                // 5.1. Send packet length first
                await stream.WriteAsync(header, 0, header.Length);

                // 5.2 Send JSON packet right after
                await stream.WriteAsync(sendByte, 0, sendByte.Length);

                // -----------------------------------------------------------------
                // --- BƯỚC MỚI: LẮNG NGHE (LISTEN) 👂 LỆNH (COMMAND) PHẢN HỒI ⬅️ ---
                // -----------------------------------------------------------------
                try
                {
                    // 1. Đặt (Set) một thời gian chờ (timeout) NGẮN ⏱️ (ví dụ: 100ms)
                    //    Nếu Server 🖥️ không trả lời (reply) ngay, 
                    //    chúng ta giả định (assume) nó không có gì để nói.
                    stream.ReadTimeout = 500; // 200 mili giây

                    // 2. Đọc (Read) 4-byte "Tiêu đề Độ dài" (Length Header) 🏷️ (Giống hệt Server 🖥️)
                    byte[] headerBuffer = new byte[4];
                    int headerBytesRead = await stream.ReadAsync(headerBuffer, 0, 4);

                    if (headerBytesRead == 4)
                        _logger.LogInformation($"[DEBUG] Received 1 command from server!");
                    {
                        // 3. Chúng ta đã nhận được một lệnh (command)! 
                        //    (Đọc (Read) phần còn lại của gói tin (packet) y như Server 🖥️)
                        int cmdPacketLength = BitConverter.ToInt32(headerBuffer, 0);
                        cmdPacketLength = IPAddress.NetworkToHostOrder(cmdPacketLength);

                        byte[] jsonBuffer = new byte[cmdPacketLength];
                        // d. SỬA LỖI: Dùng Vòng lặp "Đọc Chắc chắn" (Robust Read Loop) 🔄
                        int totalBytesRead = 0;
                        int bytesLeft = cmdPacketLength;
                        while (totalBytesRead < cmdPacketLength)
                        {
                            // SỬA LỖI: Đọc (Read) 'bytesLeft' (số byte còn lại), 
                            // KHÔNG phải 'cmdPacketLength'
                            int bytesRead = await stream.ReadAsync(jsonBuffer, totalBytesRead, bytesLeft);
                            if (bytesRead == 0) break; // Lỗi (Error), Server 🖥️ ngắt kết nối
                            totalBytesRead += bytesRead;
                            bytesLeft -= bytesRead;
                        }

                        if (totalBytesRead == cmdPacketLength)
                        {
                            // e. Giải mã (Deserialize)
                            var command = JsonSerializer.Deserialize<ServerCommand>(jsonBuffer);

                            // f. CHUYỂN GIAO (HAND OFF) 🤝
                            if (command != null)
                            {
                                _commandHandler.ExecuteCommand(command);
                            }
                        }
                    }
                }
                catch (IOException ex)
                {
                    // BẮT (CATCH) LỖI HẾT THỜI GIAN CHỜ (TIMEOUT)
                    // Đây là trường hợp "bình thường" (normal) - Server 🖥️ không có gì để nói.
                    // Chúng ta không cần làm gì cả.
                }
                finally
                {
                    // Đặt (Reset) lại thời gian chờ (timeout) về vô hạn (infinite)
                    stream.ReadTimeout = Timeout.Infinite;
                }
                // --- KẾT THÚC LOGIC LẮNG NGHE (LISTEN) ---
            }
            catch (Exception ex)
            {
                // 2.
                Console.WriteLine($"PostInformation() error: {ex.Message}");
                if (stream != null) await stream.DisposeAsync();
                tcpClient.Dispose();
                tcpClient = new TcpClient();
            }
        }
    }
}