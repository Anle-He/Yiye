using Yiye.Models;
namespace Yiye.Presentation;

public static class ScorePresentation
{
    public static string WorkScaleLabel => $"满分 {RatingScale.RankMaximum:0.0}";
    public static string AuthorScaleLabel => $"满分 {DashboardAuthorRank.ScoreMaximum:0.0} · 加权评分";
    public static string GuideTitle => $"评分说明 · 作品 {RatingScale.RankMaximum:0.0} 分 / 作者 {DashboardAuthorRank.ScoreMaximum:0.0} 分";
    public static string GuideText =>
        $"单次 Rank = (Allure × 1.5 + Immersion + Rationality + Illumination) ÷ 5，满分 {RatingScale.RankMaximum:0.0}。\n" +
        "作品综合分取已完成且四项评分完整的单次 Rank 平均值。\n" +
        $"作者分结合该作者的完整评分和全部书籍完整评分的平均值，以 2 次评分作为先验权重，再换算至 {DashboardAuthorRank.ScoreMaximum:0.0} 分；作品数和评分次数列在作者名下。";
    public static string Tier(double? rank) => RatingScale.GetPercentage(rank) switch
    {
        >= 0.8 => "gold",
        >= 0.6 => "silver",
        _ => "bronze"
    };
}
