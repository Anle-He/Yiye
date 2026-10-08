using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Yiye.Models;

namespace Yiye.Data;

public sealed class SqliteConnectionFactory(Database database)
{
    public async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(database.ConnectionString);
        try
        {
            await connection.OpenAsync();
            connection.CreateFunction<int?, int?, int?, int?, double?>(
                "calculate_rank",
                RatingScale.Calculate);
            var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            await pragma.ExecuteNonQueryAsync();
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

}
