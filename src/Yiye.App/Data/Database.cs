using System.IO;
using Microsoft.Data.Sqlite;
namespace Yiye.Data;

public sealed class Database
{
    private readonly DataPaths _paths;
    public Database(string? databasePath = null, bool pooling = true)
    {
        _paths = new(databasePath);
        ConnectionString = new SqliteConnectionStringBuilder
        { DataSource = DatabasePath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = pooling }.ToString();
    }
    public string ConnectionString { get; }
    public string DatabasePath => _paths.DatabasePath;
    public string DataDirectory => _paths.DataDirectory;
    public string CoversDirectory => _paths.CoversDirectory;
    public string MigrationBackupPath => Path.Combine(DataDirectory, $"{Path.GetFileNameWithoutExtension(DatabasePath)}.pre-v{DatabaseMigrator.CurrentSchemaVersion}.bak");
    public string GetCoverDirectory(string workId) => _paths.GetCoverDirectory(workId);
    public string GetCoverFilePath(string workId, string fileName) => _paths.GetCoverFilePath(workId, fileName);
    public Task InitializeAsync() => new DatabaseMigrator(this).InitializeAsync();
}
