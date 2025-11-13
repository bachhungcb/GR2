using Serilog.Extensions.Hosting;
using System.IO;

namespace MyWorkerService.Services;

public class AgentIdService
{
    // 1. "Bộ nhớ đệm" (cache) trong bộ nhớ (private)
    private Guid _cachedAgentId = Guid.Empty;

    // 2. Đường dẫn (path) cố định đến file ID (chỉ đọc)
    private readonly string _agentIdFilePath;
    
    // 3. Hàm khởi tạo (Constructor) - Nơi tuyệt vời để
    //    xây dựng đường dẫn (path) một lần duy nhất
    public AgentIdService()
    {
        // Lấy thư mục C:\ProgramData
        string commonDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        // Tạo một thư mục con an toàn cho agent của chúng ta
        // Ví dụ: C:\ProgramData\MySiemAgent
        string agentDirectory = Path.Combine(commonDataPath, "MySiemAgent");

        // Đảm bảo thư mục đó tồn tại
        Directory.CreateDirectory(agentDirectory);

        // Xây dựng đường dẫn (path) file đầy đủ
        // Ví dụ: C:\ProgramData\MySiemAgent\agent.id
        _agentIdFilePath = Path.Combine(agentDirectory, "agent.id");
    }

    // 4. Phương thức (method) Get() chính
    public Guid GetAgentId()
    {
        // BƯỚC A: Kiểm tra "bộ nhớ đệm" (cache) 🧠 trước
        // (Đây là logic bạn đã viết, nó hoàn hảo)
        if (!_cachedAgentId.Equals(Guid.Empty))
        {
            return _cachedAgentId;
        }

        // BƯỚC B: Bộ nhớ đệm bị trống. 
        // Chúng ta cần đọc/ghi file 💾
        try
        {
            //Đọc ghi vào file
            if (File.Exists(_agentIdFilePath))
            {
                var agentId = File.ReadAllText(_agentIdFilePath);
                _cachedAgentId = Guid.Parse(agentId);
            }
            else
            {
                var agentId = Guid.NewGuid();
                File.WriteAllText(_agentIdFilePath, agentId.ToString());
                _cachedAgentId = agentId;
            }
        }
        catch (Exception ex)
        {
            // Xử lý lỗi (ví dụ: không có quyền ghi file)
            // Tạm thời, chúng ta sẽ chỉ tạo một ID tạm thời cho phiên này
            Console.WriteLine($"[Error] Can NOT read/write agent.id at {_agentIdFilePath}: {ex.Message}");
            _cachedAgentId = Guid.NewGuid(); // Tạo ID tạm thời
        }

        return _cachedAgentId;
    }
}