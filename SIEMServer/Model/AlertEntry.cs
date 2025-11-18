namespace SIEMServer.Model;

public class AlertEntry
{
    public Guid Id { get; set; }
        
    // Thông tin cảnh báo
    public string ProcessName { get; set; }
    public int Pid { get; set; }
    public string MatchedRule { get; set; }
    public DateTime Timestamp { get; set; }

    // Liên kết với Agent (Khóa ngoại)
    public Guid AgentId { get; set; }
    public Agent Agent { get; set; }
}