using Yiye.Models;

namespace Yiye.Presentation;

public sealed class CoverCard(WorkCover value)
{
    public WorkCover Value { get; } = value;
    public string Id => Value.Id;
    public string WorkId => Value.WorkId;
    public string FileName => Value.FileName;
    public string FilePath => Value.FilePath;
    public int SortOrder => Value.SortOrder;
    public DateTimeOffset CreatedAt => Value.CreatedAt;

    public bool IsPrimary => SortOrder == 0;
    public string PositionLabel => IsPrimary ? "主封面" : $"第 {SortOrder + 1} 张";
}
