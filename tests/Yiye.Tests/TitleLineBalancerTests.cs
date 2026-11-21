using System.Globalization;
using Yiye.Presentation;

namespace Yiye.Tests;

public sealed class TitleLineBalancerTests
{
    [Theory]
    [InlineData("长城与明代朝政", 90, "长城与\n明代朝政")]
    [InlineData("新世纪福音战士", 90, "新世纪\n福音战士")]
    [InlineData("人工智能发展史", 90, "人工智能\n发展史")]
    [InlineData("中国近代思想史论", 90, "中国近代\n思想史论")]
    [InlineData("平凡世界中的普通人", 120, "平凡世界中\n的普通人")]
    public void ChineseTitles_BalanceTwoLinesAtWordBoundaries(string title, double width, string expected)
    {
        Assert.Equal(expected, TitleLineBalancer.Balance(title, width, EqualElementWidth));
    }

    [Fact]
    public void TitleThatFitsOneLine_StaysOnOneLine()
    {
        Assert.Equal("长城与明代朝政", TitleLineBalancer.Balance("长城与明代朝政", 120, EqualElementWidth));
    }

    [Fact]
    public void WholeWords_TakePriorityOverFittingAllCharactersInTwoLines()
    {
        Assert.Equal("阿尔伯特\n爱因斯坦与世界",
            TitleLineBalancer.Balance("阿尔伯特爱因斯坦与世界", 90, EqualElementWidth));
    }

    [Fact]
    public void ChangingAvailableWidth_RecomputesLineBreaks()
    {
        const string title = "长城与明代朝政";

        Assert.Equal(title, TitleLineBalancer.Balance(title, 120, EqualElementWidth));
        Assert.Equal("长城与\n明代朝政", TitleLineBalancer.Balance(title, 90, EqualElementWidth));
        Assert.Equal(title, TitleLineBalancer.Balance(title, 120, EqualElementWidth));
    }

    [Fact]
    public void LinesAreBalancedByMeasuredWidth()
    {
        static double Measure(string value) => value.Sum(character => character is '长' or '城' ? 30d : 10d);

        Assert.Equal("长城\n与明代朝政", TitleLineBalancer.Balance("长城与明代朝政", 90, Measure));
    }

    [Theory]
    [InlineData("The Silent Patient", 100, "The Silent\nPatient")]
    [InlineData("Project 2026 年鉴", 100, "Project\n2026 年鉴")]
    [InlineData("We can’t stop", 80, "We can’t\nstop")]
    public void EnglishWordsAndNumbers_StayTogether(string title, double width, string expected)
    {
        static double Measure(string value) => value.Sum(character => character == ' ' ? 5d : character < 128 ? 8d : 15d);

        Assert.Equal(expected, TitleLineBalancer.Balance(title, width, Measure));
    }

    [Theory]
    [InlineData("长城故事（明代朝政）", "长城故事\n（明代朝政）")]
    [InlineData("长城（明代）故事集", "长城（明代）\n故事集")]
    public void Parentheses_RemainWithTheirEnclosedText(string title, string expected)
    {
        Assert.Equal(expected, TitleLineBalancer.Balance(title, 90, EqualElementWidth));
    }

    [Theory]
    [InlineData("长城👩‍🔬与明代朝政", "长城👩‍🔬与\n明代朝政")]
    [InlineData("长城e\u0301与明代朝政", "长城e\u0301与\n明代朝政")]
    public void GraphemeClusters_AreKeptIntact(string title, string expected)
    {
        Assert.Equal(expected, TitleLineBalancer.Balance(title, 90, EqualElementWidth));
    }

    [Fact]
    public void LongChineseTitle_FillsFirstLineWithWholeWords()
    {
        const string title = "一部标题很长的作品，封面与标题仍应可以辨认和打开";

        Assert.Equal("一部标题很长\n的作品，封面与标题仍应可以辨认和打开",
            TitleLineBalancer.Balance(title, 90, EqualElementWidth));
    }

    [Theory]
    [InlineData("Supercalifragilisticexpialidocious")]
    [InlineData("Unbreakable")]
    public void TitlesWithoutACompleteTwoLineBreak_UseNativeWrapping(string title)
    {
        Assert.Equal(title, TitleLineBalancer.Balance(title, 90, EqualElementWidth));
    }

    private static double EqualElementWidth(string value) => StringInfo.ParseCombiningCharacters(value).Length * 15d;
}
