using System.Diagnostics;
using System.Text.Json;
using System.Management;
using System.IO;

namespace MyWorkerService.Services
{
    public sealed class GetProcessService
    {
        private readonly ILogger<GetProcessService> _logger;
        // Add a field for PostInfoService and initialize it in the constructor
        private readonly PostInfoService _postInfoService;

        // Move initialization of 'stream' from field initializer to constructor
        public struct ProcessJsonElement
        {
            public int Pid { get; set; }
            public string Name { get; set; }

            public string FilePath { get; set; }
            public string CommandLine { get; set; }

            public override string ToString() => $"{Name} ({Pid})";
        }

        public GetProcessService(
            ILogger<GetProcessService> logger,
            PostInfoService postInfoService)
        {
            _logger = logger;
            _postInfoService = postInfoService;
        }

        public List<ProcessJsonElement>? GetAllProcessData() // Đổi tên và kiểu trả về
        {
            try
            {
                var processesList = new List<ProcessJsonElement>();

                // 1. XÂY DỰNG TRUY VẤN (QUERY) WMI
                // Chúng ta yêu cầu (request) 4 thuộc tính (properties) từ bảng (table) Win32_Process
                var wmiQuery = "SELECT ProcessId, Name, ExecutablePath, CommandLine FROM Win32_Process";

                using (var searcher = new ManagementObjectSearcher(wmiQuery))
                using (var results = searcher.Get())
                {
                    // 2. DUYỆT (LOOP) QUA CÁC KẾT QUẢ WMI
                    foreach (ManagementObject mo in results)
                    {
                        try
                        {
                            // 3. TẠO "KHUÔN MẪU" (TEMPLATE) CỦA CHÚNG TA
                            var p = new ProcessJsonElement
                            {
                                // WMI ProcessId là 'uint', chúng ta ép kiểu (cast) nó về 'int'
                                Pid = (int)(uint)mo["ProcessId"],

                                // Lấy (Get) Tên (Name) (ví dụ: "Teams.exe")
                                Name = mo["Name"]?.ToString() ?? string.Empty, // <--- Sửa ở đây
    
                                // SỬA LỖI: Chuyển 'null' thành 'string.Empty'
                                FilePath = mo["ExecutablePath"]?.ToString() ?? string.Empty, 
                                CommandLine = mo["CommandLine"]?.ToString() ?? string.Empty
                            };

                            processesList.Add(p);
                        }
                        catch (Exception ex)
                        {
                            // Bỏ qua (Skip) tiến trình (process) cụ thể này nếu có lỗi 
                            // (ví dụ: tiến trình (process) vừa "chết" (died) trong lúc truy vấn)
                            _logger.LogWarning($"Không thể lấy (fetch) WMI cho một tiến trình: {ex.Message}");
                        }
                    }
                }

                // 4. Trả về (Return) danh sách (List) đã được "làm giàu" (enriched)
                return processesList;
            }
            catch (Exception ex)
            {
                // Ghi log (Log) nếu toàn bộ WMI thất bại
                _logger.LogError(ex, "Lỗi nghiêm trọng khi truy vấn (query) WMI Win32_Process");
                return null;
            }
        }
    }
}