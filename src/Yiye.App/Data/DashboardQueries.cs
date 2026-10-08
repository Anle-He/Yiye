using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Yiye.Models;

namespace Yiye.Data;

public sealed class DashboardQueries(Database database) : SqliteStore(database)
{
    public async Task<IReadOnlyList<DashboardTimelineItem>> GetRecentTimelineAsync(int limit = 5)
    {
        var items = new List<DashboardTimelineItem>();
        await using var connection = await OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT e.id, e.work_id, w.title, w.kind, e.completed_on, e.notes,
                   (SELECT c.file_name FROM work_covers c WHERE c.work_id = w.id
                    ORDER BY c.sort_order, c.created_at LIMIT 1) AS primary_cover_file
            FROM experiences e
            JOIN works w ON w.id = e.work_id
            WHERE e.completed_on IS NOT NULL
            ORDER BY e.completed_on DESC, e.updated_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 8));
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var primaryCoverPath = ReadCoverPath(reader, 1, 6);

            items.Add(new DashboardTimelineItem
            {
                Id = reader.GetString(0),
                WorkId = reader.GetString(1),
                Title = reader.GetString(2),
                Kind = reader.GetString(3),
                LoggedOn = DateOnly.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
                Notes = reader.IsDBNull(5) ? null : reader.GetString(5),
                PrimaryCoverPath = primaryCoverPath
            });
        }
        return items;
    }

    public async Task<DashboardShowcase> GetDashboardShowcaseAsync()
    {
        var works = new List<DashboardShowcaseItem>();
        var authors = new List<DashboardAuthorRank>();
        await using var connection = await OpenAsync();

        var worksCommand = connection.CreateCommand();
        worksCommand.CommandText = """
            SELECT w.id, w.title, w.kind, w.author,
                   COUNT(e.id) AS completion_count,
                   SUM(CASE WHEN e.allure IS NOT NULL AND e.immersion IS NOT NULL
                                 AND e.rationality IS NOT NULL AND e.illumination IS NOT NULL
                            THEN 1 ELSE 0 END) AS rating_count,
                   AVG(calculate_rank(e.allure, e.immersion, e.rationality, e.illumination)) AS aggregate_rank,
                   MIN(e.completed_on) AS first_completed_on,
                   MAX(e.completed_on) AS latest_completed_on,
                   (SELECT c.file_name FROM work_covers c WHERE c.work_id = w.id
                    ORDER BY c.sort_order, c.created_at LIMIT 1) AS primary_cover_file
            FROM works w
            JOIN experiences e ON e.work_id = w.id AND e.completed_on IS NOT NULL
            GROUP BY w.id
            ORDER BY latest_completed_on DESC, w.title;
            """;
        await using (var reader = await worksCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var primaryCoverPath = ReadCoverPath(reader, 0, 9);
                works.Add(new DashboardShowcaseItem
                {
                    WorkId = reader.GetString(0),
                    Title = reader.GetString(1),
                    Kind = reader.GetString(2),
                    Author = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CompletionCount = reader.GetInt32(4),
                    RatingCount = reader.GetInt32(5),
                    AggregateRank = reader.IsDBNull(6) ? null : Math.Round(reader.GetDouble(6), 1, MidpointRounding.AwayFromZero),
                    FirstCompletedOn = DateOnly.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
                    LatestCompletedOn = DateOnly.Parse(reader.GetString(8), CultureInfo.InvariantCulture),
                    PrimaryCoverPath = primaryCoverPath
                });
            }
        }

        var authorsCommand = connection.CreateCommand();
        authorsCommand.CommandText = """
            WITH rated AS (
                SELECT w.id AS work_id, TRIM(w.author) AS author,
                       calculate_rank(e.allure, e.immersion, e.rationality, e.illumination) AS rank
                FROM experiences e
                JOIN works w ON w.id = e.work_id
                WHERE w.kind = 'book' AND e.completed_on IS NOT NULL
                  AND TRIM(COALESCE(w.author, '')) <> ''
                  AND e.allure IS NOT NULL AND e.immersion IS NOT NULL
                  AND e.rationality IS NOT NULL AND e.illumination IS NOT NULL
            ), global_mean AS (
                SELECT AVG(rank) AS mean_rank FROM rated
            ), author_stats AS (
                SELECT author, COUNT(DISTINCT work_id) AS work_count,
                       COUNT(*) AS rating_count, AVG(rank) AS mean_rank
                FROM rated
                GROUP BY author
            )
            SELECT author, work_count, rating_count,
                   ((rating_count * author_stats.mean_rank) + (2.0 * global_mean.mean_rank)) / (rating_count + 2.0) AS weighted_rank
            FROM author_stats CROSS JOIN global_mean
            ORDER BY weighted_rank DESC, rating_count DESC, author
            LIMIT 3;
            """;
        await using (var reader = await authorsCommand.ExecuteReaderAsync())
        {
            var position = 1;
            while (await reader.ReadAsync())
            {
                authors.Add(new DashboardAuthorRank
                {
                    Position = position++,
                    Author = reader.GetString(0),
                    WorkCount = reader.GetInt32(1),
                    RatingCount = reader.GetInt32(2),
                    WeightedRank = Math.Round(reader.GetDouble(3) * DashboardAuthorRank.ScoreMaximum / RatingScale.RankMaximum,
                        1, MidpointRounding.AwayFromZero)
                });
            }
        }

        return new DashboardShowcase { CompletedWorks = works, TopAuthors = authors };
    }

}
