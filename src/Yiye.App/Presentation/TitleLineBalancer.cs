using System.Globalization;
using System.Text;

namespace Yiye.Presentation;

public static class TitleLineBalancer
{
    public static string Balance(string text, double availableWidth, Func<string, double> measureWidth)
    {
        if (string.IsNullOrEmpty(text) || !double.IsFinite(availableWidth) || availableWidth <= 0
            || text.Contains('\n') || text.Contains('\r') || measureWidth(text) <= availableWidth)
            return text;

        var boundaries = StringInfo.ParseCombiningCharacters(text);
        var wordBoundaries = TitleWordBoundaries.Find(text);
        var bestDifference = double.PositiveInfinity;
        var bestPriority = int.MaxValue;
        string? balanced = null;
        string? wordWrapped = null;
        for (var index = 1; index < boundaries.Length; index++)
        {
            var first = text[..boundaries[index]].TrimEnd();
            var second = text[boundaries[index]..].TrimStart();
            if (first.Length == 0 || second.Length == 0) continue;
            var firstWidth = measureWidth(first);
            if (firstWidth > availableWidth) break;
            if (!CanBreak(first, second, char.IsWhiteSpace(text[boundaries[index] - 1])
                || char.IsWhiteSpace(text[boundaries[index]]))) continue;
            var atWordBoundary = wordBoundaries?.Contains(boundaries[index]) ?? true;
            if (atWordBoundary) wordWrapped = first + "\n" + second;
            var secondWidth = measureWidth(second);
            if (secondWidth > availableWidth) continue;

            var priority = atWordBoundary ? 0 : 1;
            var difference = Math.Abs(firstWidth - secondWidth);
            if (priority > bestPriority || (priority == bestPriority &&
                (difference > bestDifference || (difference == bestDifference && firstWidth < secondWidth)))) continue;
            bestPriority = priority;
            bestDifference = difference;
            balanced = first + "\n" + second;
        }

        // Longer titles fill the first line with whole words; TextBlock trims the remainder.
        return bestPriority == 0 ? balanced! : wordWrapped ?? balanced ?? text;
    }

    private static bool CanBreak(string first, string second, bool atSpace)
    {
        var before = Rune.GetRuneAt(first, StringInfo.ParseCombiningCharacters(first)[^1]);
        var after = Rune.GetRuneAt(second, 0);
        if ("（《〈“‘「『【([{\"'’".Contains(before.ToString(), StringComparison.Ordinal)
            || "，。！？；：、）》〉”’」』】)]},.!?;:%…-'’".Contains(after.ToString(), StringComparison.Ordinal))
            return false;
        return atSpace || !(IsWordCharacter(before) && IsWordCharacter(after));
    }

    private static bool IsWordCharacter(Rune value) => Rune.IsLetterOrDigit(value) && !IsEastAsian(value.Value);

    private static bool IsEastAsian(int value) => value is >= 0x2E80 and <= 0x9FFF
        or >= 0xAC00 and <= 0xD7AF or >= 0xF900 and <= 0xFAFF
        or >= 0x20000 and <= 0x323AF;
}
