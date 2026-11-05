namespace SatBackupToolkit.Models;

public sealed class BackupManifest
{
    public string Application { get; set; } = "SatBackupToolkit";
    public string CreatedAtUtc { get; set; } = string.Empty;
    public string SourceDirectory { get; set; } = string.Empty;
    public List<BackupFileEntry> Files { get; set; } = new();
}

public sealed class BackupFileEntry
{
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTime LastWriteTimeUtc { get; set; }
}

public sealed class BackupVerificationResult
{
    public bool IsValid { get; set; }
    public int FilesChecked { get; set; }
    public List<string> Messages { get; set; } = new();
}
