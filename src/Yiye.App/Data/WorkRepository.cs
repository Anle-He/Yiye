using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Yiye.Models;

namespace Yiye.Data;

public sealed class WorkRepository(Database database, CoverRepository covers) : SqliteStore(database)
{
    private static readonly string WorkSelect = $$"""
        SELECT w.id, w.title, w.subtitle, w.kind,
               {{WorkStatusSql.ForWork("w.id")}} AS status,
               w.total_episodes, w.created_at, w.updated_at,
               SUM(CASE WHEN e.completed_on IS NOT NULL THEN 1 ELSE 0 END) AS experience_count,
               SUM(CASE WHEN e.started_on IS NOT NULL AND e.completed_on IS NULL THEN 1 ELSE 0 END) AS active_count,
               SUM(CASE WHEN e.completed_on IS NOT NULL AND e.allure IS NOT NULL AND e.immersion IS NOT NULL
                             AND e.rationality IS NOT NULL AND e.illumination IS NOT NULL
                        THEN 1 ELSE 0 END) AS rated_count,
               ROUND(AVG(CASE WHEN e.completed_on IS NOT NULL
                              THEN calculate_rank(e.allure, e.immersion, e.rationality, e.illumination) END), 1) AS aggregate_rank,
               MAX(e.completed_on) AS latest_activity,
               (SELECT c.file_name FROM work_covers c WHERE c.work_id = w.id
                 ORDER BY c.sort_order, c.created_at LIMIT 1) AS primary_cover_file,
               w.author
        FROM works w
        LEFT JOIN experiences e ON e.work_id = w.id
        """;

    public async Task<IReadOnlyList<MediaWork>> GetWorksAsync()
    {
        var works = new List<MediaWork>();
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = WorkSelect + "\n" + """
            GROUP BY w.id
            ORDER BY COALESCE(latest_activity, substr(w.created_at, 1, 10)) DESC, w.created_at DESC;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            works.Add(ReadWork(reader));
        }
        return works;
    }

    public async Task<MediaWork?> GetWorkAsync(string id)
    {
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = WorkSelect + "\n" + """
            WHERE w.id = $id
            GROUP BY w.id;
            """;
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadWork(reader) : null;
    }

    public async Task AddWorkAsync(MediaWork work)
    {
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO works (id, title, subtitle, author, kind, status, total_episodes, created_at, updated_at)
            VALUES ($id, $title, $subtitle, $author, $kind, $status, $totalEpisodes, $createdAt, $updatedAt);
            """;
        command.Parameters.AddWithValue("$id", work.Id);
        command.Parameters.AddWithValue("$title", work.Title);
        command.Parameters.AddWithValue("$subtitle", (object?)work.Subtitle ?? DBNull.Value);
        command.Parameters.AddWithValue("$author", work.Kind == "book" ? (object?)work.Author ?? DBNull.Value : DBNull.Value);
        command.Parameters.AddWithValue("$kind", work.Kind);
        command.Parameters.AddWithValue("$status", (object?)work.Status ?? DBNull.Value);
        command.Parameters.AddWithValue("$totalEpisodes", work.TotalEpisodes ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", work.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", work.UpdatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateWorkMetadataAsync(MediaWork work)
    {
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE works
            SET title = $title, subtitle = $subtitle, author = $author, kind = $kind, updated_at = $updatedAt
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", work.Id);
        command.Parameters.AddWithValue("$title", work.Title);
        command.Parameters.AddWithValue("$subtitle", (object?)work.Subtitle ?? DBNull.Value);
        command.Parameters.AddWithValue("$author", work.Kind == "book" ? (object?)work.Author ?? DBNull.Value : DBNull.Value);
        command.Parameters.AddWithValue("$kind", work.Kind);
        command.Parameters.AddWithValue("$updatedAt", work.UpdatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    private MediaWork ReadWork(SqliteDataReader reader)
    {
        double? aggregate = reader.IsDBNull(11) ? null : reader.GetDouble(11);
        var primaryCoverPath = ReadCoverPath(reader, 0, 13);
        return new MediaWork
        {
            Id = reader.GetString(0),
            Title = reader.GetString(1),
            Subtitle = reader.IsDBNull(2) ? null : reader.GetString(2),
            Author = reader.IsDBNull(14) ? null : reader.GetString(14),
            Kind = reader.GetString(3),
            Status = reader.IsDBNull(4) ? null : reader.GetString(4),
            TotalEpisodes = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            PrimaryCoverPath = primaryCoverPath,
            CreatedAt = DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
            ExperienceCount = reader.GetInt32(8),
            HasActiveExperience = reader.GetInt32(9) > 0,
            RatedExperienceCount = reader.GetInt32(10),
            AggregateRank = aggregate is null ? null : Math.Round(aggregate.Value, 1, MidpointRounding.AwayFromZero),
            LatestActivityOn = reader.IsDBNull(12) ? null : DateOnly.Parse(reader.GetString(12), CultureInfo.InvariantCulture)
        };
    }

    public Task DeleteWorkAsync(string workId) => covers.DeleteWorkCoversAsync(workId, async () =>
    {
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM works WHERE id=$id;";
        command.Parameters.AddWithValue("$id", workId);
        await command.ExecuteNonQueryAsync();
    });
}
