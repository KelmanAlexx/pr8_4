namespace SatBackupToolkit.Models;

public enum TestResult
{
    Passed,
    Failed,
    Blocked,
    NotRun
}

public sealed class SatTestCase
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Preconditions { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public string ExpectedResult { get; set; } = string.Empty;
    public string ActualResult { get; set; } = string.Empty;
    public TestResult Result { get; set; } = TestResult.NotRun;
    public string Comments { get; set; } = string.Empty;
}
