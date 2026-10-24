using Yiye.Models;
namespace Yiye.Presentation;

public static class ScorePresentation
{
    public static string WorkScaleLabel => $"满分 {RatingScale.RankMaximum:0.0}";
    public static string AuthorScaleLabel => $"满分 {DashboardAuthorRank.ScoreMaximum:0.0} · 作品等权";
    public static string GuideTitle => $"评分说明 · 作品 {RatingScale.RankMaximum:0.0} 分 / 作者 {DashboardAuthorRank.ScoreMaximum:0.0} 分";
    public static string GuideText =>
        $"单次 Rank = (Allure × 1.5 + Immersion + Rationality + Illumination) ÷ 5，满分 {RatingScale.RankMaximum:0.0}。\n" +
        "作品综合分取已完成且四项评分完整的单次 Rank 平均值。\n" +
        $"作者分 = 该作者各本书综合分的平均值 ÷ {RatingScale.RankMaximum:0.0} × {DashboardAuthorRank.ScoreMaximum:0.0}，每本书等权，保留一位小数。仅纳入已完成且有完整评分的书籍；作品数和评分次数列在作者名下。";
    public static string Tier(double? rank) => RatingScale.GetPercentage(rank) switch
    {
        >= 0.8 => "gold",
        >= 0.6 => "silver",
        _ => "bronze"
    };
}
