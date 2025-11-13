namespace SIEMServer.Model;

public class BlacklistedProcess
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string FilePath { get; set; }
    public string? Commandline { get; set; }
    public string? HashValue { get; set; }
}