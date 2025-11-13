namespace MyWorkerService.Command;

public class ReceivedServerCommand
{
    public string CommandType { get; set; }
    public string Target { get; set; }
}