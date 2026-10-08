using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Yiye.Models;

namespace Yiye.Data;

public sealed class ExperienceRepository(Database database) : SqliteStore(database)
{
    private const string ExperienceSelect = """
        SELECT e.id, e.work_id, e.started_on, e.completed_on, e.allure, e.immersion, e.rationality, e.illumination,
               e.notes, e.created_at, e.updated_at,
               COUNT(p.id)
        FROM experiences e
        LEFT JOIN progress_entries p ON p.experience_id = e.id
        """;

    public async Task<IReadOnlyList<MediaExperience>> GetExperiencesAsync(string workId)
    {
        var experiences = new List<MediaExperience>();
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = ExperienceSelect + "\n" + """
            WHERE e.work_id = $workId
            GROUP BY e.id
            ORDER BY COALESCE(e.completed_on, e.started_on, substr(e.created_at, 1, 10)) DESC, e.created_at DESC;
            """;
        command.Parameters.AddWithValue("$workId", workId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            experiences.Add(ReadExperience(reader));
        }
        return experiences;
    }

    public async Task<IReadOnlyList<HistoricalProgressEntry>> GetHistoricalProgressAsync(string experienceId)
    {
        var entries = new List<HistoricalProgressEntry>();
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT logged_on, metric, amount, notes FROM progress_entries
            WHERE experience_id = $experienceId ORDER BY logged_on, created_at, id;
            """;
        command.Parameters.AddWithValue("$experienceId", experienceId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            entries.Add(new HistoricalProgressEntry(
                DateOnly.Parse(reader.GetString(0), CultureInfo.InvariantCulture),
                reader.GetString(1), reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
        return entries;
    }

    public async Task AddExperienceAsync(MediaExperience experience)
    {
        ExperienceRules.Validate(experience);
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var insert = connection.CreateCommand();
        insert.Transaction = (SqliteTransaction)transaction;
        insert.CommandText = """
            INSERT INTO experiences
                (id, work_id, started_on, completed_on, allure, immersion, rationality, illumination, notes, created_at, updated_at)
            VALUES
                ($id, $workId, $startedOn, $completedOn, $allure, $immersion, $rationality, $illumination, $notes, $createdAt, $updatedAt);
            """;
        insert.Parameters.AddWithValue("$id", experience.Id);
        insert.Parameters.AddWithValue("$workId", experience.WorkId);
        insert.Parameters.AddWithValue("$startedOn", experience.StartedOn?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$completedOn", experience.CompletedOn?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$allure", experience.Allure ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$immersion", experience.Immersion ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$rationality", experience.Rationality ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$illumination", experience.Illumination ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$notes", (object?)experience.Notes ?? DBNull.Value);
        insert.Parameters.AddWithValue("$createdAt", experience.CreatedAt.ToString("O"));
        insert.Parameters.AddWithValue("$updatedAt", experience.UpdatedAt.ToString("O"));
        await insert.ExecuteNonQueryAsync();

        await RefreshWorkStatusAsync(
            connection,
            (SqliteTransaction)transaction,
            experience.WorkId,
            experience.UpdatedAt);
        await transaction.CommitAsync();
    }

    public async Task UpdateExperienceAsync(MediaExperience experience)
    {
        ExperienceRules.Validate(experience);
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            UPDATE experiences SET started_on=$startedOn, completed_on=$completedOn,
                allure=$allure, immersion=$immersion, rationality=$rationality, illumination=$illumination,
                notes=$notes, updated_at=$updatedAt WHERE id=$id AND work_id=$workId;
            """;
        command.Parameters.AddWithValue("$id", experience.Id);
        command.Parameters.AddWithValue("$workId", experience.WorkId);
        command.Parameters.AddWithValue("$startedOn", experience.StartedOn?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$completedOn", experience.CompletedOn?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$allure", experience.Allure ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$immersion", experience.Immersion ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$rationality", experience.Rationality ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$illumination", experience.Illumination ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)experience.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", experience.UpdatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync();

        await RefreshWorkStatusAsync(
            connection,
            (SqliteTransaction)transaction,
            experience.WorkId,
            experience.UpdatedAt);
        await transaction.CommitAsync();
    }

    public async Task DeleteExperienceAsync(string experienceId, string workId)
    {
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var delete = connection.CreateCommand();
        delete.Transaction = (SqliteTransaction)transaction;
        delete.CommandText = "DELETE FROM experiences WHERE id=$id AND work_id=$workId;";
        delete.Parameters.AddWithValue("$id", experienceId);
        delete.Parameters.AddWithValue("$workId", workId);
        await delete.ExecuteNonQueryAsync();

        await RefreshWorkStatusAsync(
            connection,
            (SqliteTransaction)transaction,
            workId,
            DateTimeOffset.Now);
        await transaction.CommitAsync();
    }

    private static async Task RefreshWorkStatusAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string workId,
        DateTimeOffset updatedAt)
    {
        var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = $$"""
            UPDATE works SET
                status = {{WorkStatusSql.ForWork("$workId")}},
                updated_at=$updatedAt
            WHERE id=$workId;
            """;
        update.Parameters.AddWithValue("$workId", workId);
        update.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O"));
        await update.ExecuteNonQueryAsync();
    }

    private static MediaExperience ReadExperience(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        WorkId = reader.GetString(1),
        StartedOn = reader.IsDBNull(2) ? null : DateOnly.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
        CompletedOn = reader.IsDBNull(3) ? null : DateOnly.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
        Allure = reader.IsDBNull(4) ? null : reader.GetInt32(4),
        Immersion = reader.IsDBNull(5) ? null : reader.GetInt32(5),
        Rationality = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        Illumination = reader.IsDBNull(7) ? null : reader.GetInt32(7),
        Notes = reader.IsDBNull(8) ? null : reader.GetString(8),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture),
        UpdatedAt = DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture),
        ProgressEntryCount = reader.GetInt32(11)
    };
}
