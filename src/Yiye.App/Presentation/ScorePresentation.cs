using Yiye.Models;
namespace Yiye.Presentation;

public static class ScorePresentation
{
    public static string Tier(double? rank) => RatingScale.GetPercentage(rank) switch
    {
        >= 0.8 => "gold",
        >= 0.6 => "silver",
        _ => "bronze"
    };
}
