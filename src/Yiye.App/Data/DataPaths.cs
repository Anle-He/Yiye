using System.IO;
namespace Yiye.Data;

public sealed class DataPaths
{
    public DataPaths(string? databasePath = null)
    {
        DatabasePath = Path.GetFullPath(databasePath ?? GetDefaultPath());
        DataDirectory = Path.GetDirectoryName(DatabasePath)!;
        CoversDirectory = Path.Combine(DataDirectory, "covers");
    }
    public string DatabasePath { get; }
    public string DataDirectory { get; }
    public string CoversDirectory { get; }
    public string GetCoverDirectory(string workId)
    {
        if (string.IsNullOrWhiteSpace(workId) || workId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("Invalid work identifier for cover storage.");
        }

        var root = Path.GetFullPath(CoversDirectory) + Path.DirectorySeparatorChar;
        var directory = Path.GetFullPath(Path.Combine(CoversDirectory, workId));
        if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cover directory escaped the data root.");
        }
        return directory;
    }

    public string GetCoverFilePath(string workId, string fileName)
    {
        if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid cover file name.");
        }
        return Path.Combine(GetCoverDirectory(workId), fileName);
    }

    private static string GetDefaultPath()
    {
        var overrideDirectory = Environment.GetEnvironmentVariable("YIYE_DATA_DIR");
        if (string.IsNullOrWhiteSpace(overrideDirectory))
            overrideDirectory = Environment.GetEnvironmentVariable("QUIETSHELF_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overrideDirectory))
        {
            return Path.Combine(overrideDirectory, "records.db");
        }

        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        // Preserve the installed library location across the product rename.
        return Path.Combine(root, "QuietShelf", "records.db");
    }
}
