using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Yiye.Models;

namespace Yiye.Data;

public abstract class SqliteStore(Database database)
{
    protected Database Database { get; } = database;
    protected Task<SqliteConnection> OpenAsync() => new SqliteConnectionFactory(Database).OpenAsync();

    protected string? ReadCoverPath(SqliteDataReader reader, int workIdColumn, int fileNameColumn)
    {
        if (reader.IsDBNull(fileNameColumn)) return null;
        var path = Database.GetCoverFilePath(reader.GetString(workIdColumn), reader.GetString(fileNameColumn));
        return File.Exists(path) ? path : null;
    }
}
