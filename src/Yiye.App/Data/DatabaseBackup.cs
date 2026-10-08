using System.IO;
using Microsoft.Data.Sqlite;
namespace Yiye.Data;

internal static class DatabaseBackup
{
    public static void Create(SqliteConnection source, string backupPath)
    {
        var temporaryPath = backupPath + ".new";
        File.Delete(temporaryPath);
        var backupConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = temporaryPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
        try
        {
            using (var destination = new SqliteConnection(backupConnectionString))
            {
                destination.Open();
                source.BackupDatabase(destination);

                using var integrityCheck = destination.CreateCommand();
                integrityCheck.CommandText = "PRAGMA integrity_check;";
                var result = Convert.ToString(integrityCheck.ExecuteScalar());
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("The migration backup failed its integrity check.");
                }
            }

            if (File.Exists(backupPath))
            {
                File.Replace(temporaryPath, backupPath, null);
            }
            else
            {
                File.Move(temporaryPath, backupPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

}
