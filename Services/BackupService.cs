using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using SatBackupToolkit.Models;

namespace SatBackupToolkit.Services;

public sealed class BackupService
{
    private const string ManifestName = "__sat_backup_manifest__.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string CreateBackup(string sourceDirectory, string backupDirectory)
    {
        string source = Path.GetFullPath(sourceDirectory);
        string destination = Path.GetFullPath(backupDirectory);

        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException($"Папка-источник не найдена: {source}");

        if (IsSameOrChildDirectory(destination, source))
            throw new InvalidOperationException("Папка резервных копий не должна находиться внутри папки-источника.");

        Directory.CreateDirectory(destination);

        string archivePath = Path.Combine(destination,
            $"backup_{DateTime.Now:yyyyMMdd_HHmmss_fff}.zip");

        var manifest = new BackupManifest
        {
            CreatedAtUtc = DateTime.UtcNow.ToString("O"),
            SourceDirectory = source
        };

        try
        {
            using (FileStream fileStream = new(archivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (ZipArchive archive = new(fileStream, ZipArchiveMode.Create))
            {
                foreach (string filePath in EnumerateRegularFiles(source))
                {
                    string relativePath = Path.GetRelativePath(source, filePath)
                        .Replace(Path.DirectorySeparatorChar, '/');

                    if (relativePath.Equals(ManifestName, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"Имя файла {ManifestName} зарезервировано для манифеста.");

                    FileInfo info = new(filePath);
                    ZipArchiveEntry entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                    entry.LastWriteTime = ClampZipDate(info.LastWriteTime);

                    using Stream input = File.OpenRead(filePath);
                    using Stream output = entry.Open();
                    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    byte[] buffer = new byte[81920];
                    long totalBytes = 0;
                    int bytesRead;

                    while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, bytesRead);
                        hash.AppendData(buffer, 0, bytesRead);
                        totalBytes += bytesRead;
                    }

                    manifest.Files.Add(new BackupFileEntry
                    {
                        Path = relativePath,
                        Size = totalBytes,
                        Sha256 = Convert.ToHexString(hash.GetHashAndReset()),
                        LastWriteTimeUtc = info.LastWriteTimeUtc
                    });
                }

                ZipArchiveEntry manifestEntry = archive.CreateEntry(ManifestName, CompressionLevel.Optimal);
                using Stream manifestStream = manifestEntry.Open();
                JsonSerializer.Serialize(manifestStream, manifest, JsonOptions);
            }

            return archivePath;
        }
        catch
        {
            if (File.Exists(archivePath))
                File.Delete(archivePath);
            throw;
        }
    }

    public BackupVerificationResult VerifyBackup(string archivePath)
    {
        var result = new BackupVerificationResult { IsValid = false };
        string fullArchivePath = Path.GetFullPath(archivePath);

        if (!File.Exists(fullArchivePath))
        {
            result.Messages.Add("Архив не найден.");
            return result;
        }

        try
        {
            using FileStream fileStream = File.OpenRead(fullArchivePath);
            using ZipArchive archive = new(fileStream, ZipArchiveMode.Read);

            List<ZipArchiveEntry> manifestEntries = archive.Entries
                .Where(entry => string.Equals(entry.FullName, ManifestName, StringComparison.Ordinal))
                .ToList();

            if (manifestEntries.Count != 1)
            {
                result.Messages.Add("В архиве отсутствует манифест или он встречается несколько раз.");
                return result;
            }

            BackupManifest? manifest;
            using (Stream stream = manifestEntries[0].Open())
                manifest = JsonSerializer.Deserialize<BackupManifest>(stream, JsonOptions);

            if (manifest == null || manifest.Files == null)
            {
                result.Messages.Add("Не удалось прочитать манифест резервной копии.");
                return result;
            }

            var expectedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (BackupFileEntry expected in manifest.Files)
            {
                if (!IsSafeRelativePath(expected.Path) || !expectedPaths.Add(expected.Path))
                {
                    result.Messages.Add($"В манифесте обнаружен некорректный или повторяющийся путь: {expected.Path}");
                    return result;
                }
            }

            var archiveFiles = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName == ManifestName)
                    continue;

                if (!IsSafeRelativePath(entry.FullName) || !archiveFiles.TryAdd(entry.FullName, entry))
                {
                    result.Messages.Add($"В архиве обнаружен некорректный или повторяющийся путь: {entry.FullName}");
                    return result;
                }
            }

            if (!expectedPaths.SetEquals(archiveFiles.Keys))
            {
                result.Messages.Add("Список файлов в архиве не совпадает со списком в манифесте.");
                return result;
            }

            bool allValid = true;
            foreach (BackupFileEntry expected in manifest.Files)
            {
                ZipArchiveEntry entry = archiveFiles[expected.Path];
                using Stream stream = entry.Open();
                using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[81920];
                long totalBytes = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hash.AppendData(buffer, 0, bytesRead);
                    totalBytes += bytesRead;
                }

                string actualHash = Convert.ToHexString(hash.GetHashAndReset());
                bool valid = totalBytes == expected.Size &&
                    string.Equals(actualHash, expected.Sha256, StringComparison.OrdinalIgnoreCase);

                result.FilesChecked++;
                result.Messages.Add(valid
                    ? $"[OK] {expected.Path}"
                    : $"[ОШИБКА] {expected.Path}: размер или SHA-256 не совпадает.");
                allValid &= valid;
            }

            result.IsValid = allValid;
            result.Messages.Add(allValid
                ? "Проверка завершена: архив целый, контрольные суммы совпадают."
                : "Проверка завершена: найдены ошибки целостности.");
            return result;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            result.Messages.Add("Ошибка проверки: " + ex.Message);
            return result;
        }
    }

    public void RestoreBackup(string archivePath, string targetDirectory)
    {
        BackupVerificationResult verification = VerifyBackup(archivePath);
        if (!verification.IsValid)
            throw new InvalidOperationException("Восстановление запрещено: архив не прошёл проверку SHA-256.");

        string target = Path.GetFullPath(targetDirectory);
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
            throw new InvalidOperationException("Папка восстановления должна быть пустой.");

        Directory.CreateDirectory(target);
        string targetRoot = Path.TrimEndingDirectorySeparator(target) + Path.DirectorySeparatorChar;

        using FileStream fileStream = File.OpenRead(archivePath);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Read);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (entry.FullName == ManifestName)
                continue;

            if (!IsSafeRelativePath(entry.FullName))
                throw new InvalidDataException("В архиве обнаружен небезопасный путь.");

            string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            string outputPath = Path.GetFullPath(Path.Combine(target, relative));
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (!outputPath.StartsWith(targetRoot, comparison))
                throw new InvalidDataException("Путь файла выходит за пределы папки восстановления.");

            string? parent = Path.GetDirectoryName(outputPath);
            if (parent != null)
                Directory.CreateDirectory(parent);

            using Stream input = entry.Open();
            using FileStream output = new(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
    }

    private static IEnumerable<string> EnumerateRegularFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            string current = pending.Pop();

            foreach (string directory in Directory.EnumerateDirectories(current))
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0)
                    pending.Push(directory);
            }

            foreach (string file in Directory.EnumerateFiles(current))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0)
                    yield return file;
            }
        }
    }

    private static bool IsSameOrChildDirectory(string candidate, string root)
    {
        string normalizedRoot = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        string normalizedCandidate = Path.TrimEndingDirectorySeparator(candidate) + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return normalizedCandidate.StartsWith(normalizedRoot, comparison) ||
               string.Equals(Path.TrimEndingDirectorySeparator(candidate),
                   Path.TrimEndingDirectorySeparator(root), comparison);
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\'))
            return false;

        string[] parts = path.Split('/', StringSplitOptions.None);
        return parts.All(part => part.Length > 0 && part != "." && part != "..");
    }

    private static DateTimeOffset ClampZipDate(DateTime value)
    {
        DateTime safe = value < new DateTime(1980, 1, 1) ? new DateTime(1980, 1, 1) : value;
        return new DateTimeOffset(safe);
    }
}
