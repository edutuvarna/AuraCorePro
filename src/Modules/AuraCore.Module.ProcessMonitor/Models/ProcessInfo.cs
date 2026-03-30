namespace AuraCore.Module.ProcessMonitor.Models;

public sealed class ProcessInfo
{
    public int    Pid           { get; set; }
    public string Name          { get; set; } = "";
    public string Description   { get; set; } = "";
    public string FilePath      { get; set; } = "";
    public double CpuPercent    { get; set; }
    public long   MemoryMb      { get; set; }
    public string Status        { get; set; } = "Running";
    public int    Priority      { get; set; } = 8; // Normal
    public string PriorityLabel { get; set; } = "Normal";
    public int    ThreadCount   { get; set; }
    public int    HandleCount   { get; set; }
    public DateTime StartTime   { get; set; }
    public string UserName      { get; set; } = "";
}

public sealed class ProcessReport
{
    public List<ProcessInfo> Processes { get; set; } = new();
    public double TotalCpuPercent      => Processes.Sum(p => p.CpuPercent);
    public long   TotalMemoryMb        => Processes.Sum(p => p.MemoryMb);
}
