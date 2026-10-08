namespace Yiye.Data;

internal static class WorkStatusSql
{
    public static string ForWork(string workIdExpression) => $$"""
        CASE
            WHEN EXISTS (SELECT 1 FROM experiences WHERE work_id={{workIdExpression}}
                         AND started_on IS NOT NULL AND completed_on IS NULL) THEN 'in_progress'
            WHEN EXISTS (SELECT 1 FROM experiences WHERE work_id={{workIdExpression}}
                         AND completed_on IS NOT NULL) THEN 'completed'
            ELSE 'planned'
        END
        """;
}
