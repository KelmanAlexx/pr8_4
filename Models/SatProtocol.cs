namespace SatBackupToolkit.Models;

public sealed class SatProtocol
{
    public string ProjectName { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Site { get; set; } = string.Empty;
    public string Tester { get; set; } = string.Empty;
    public string CustomerRepresentative { get; set; } = string.Empty;
    public DateTime TestDate { get; set; } = DateTime.Today;
    public List<SatTestCase> TestCases { get; set; } = new();
}
