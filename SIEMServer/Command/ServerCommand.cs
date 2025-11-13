namespace SIEMServer.Command;

public class ServerCommand
{
    public string CommandType { get; set; } //Ex: "BLOCK_PROCESS_ID"
    public string Target { get; set; } // EX: "12345" (PID)
}