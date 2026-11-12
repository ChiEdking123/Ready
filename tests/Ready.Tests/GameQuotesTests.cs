using Ready.Models;
using Xunit;

namespace Ready.Tests;

public sealed class GameQuotesTests
{
    [Fact]
    public void Every_quote_has_a_line_an_author_and_a_source()
    {
        Assert.NotEmpty(GameQuotes.All);
        foreach (var quote in GameQuotes.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(quote.Text));
            Assert.False(string.IsNullOrWhiteSpace(quote.Author));
            Assert.False(string.IsNullOrWhiteSpace(quote.Source));
        }
    }

    // 手机上的横幅最多三行，正文超过这个长度就会被截断成半句话。
    [Fact]
    public void Quotes_stay_short_enough_for_three_lines_on_a_phone()
    {
        Assert.All(GameQuotes.All, quote => Assert.True(quote.Text.Length <= 48, quote.Text));
    }

    // 语录来自不同的开发者，而不是同一个人说了十二遍。
    [Fact]
    public void Quotes_come_from_a_range_of_developers()
    {
        var authors = GameQuotes.All.Select(q => q.Author).Distinct().Count();
        Assert.True(authors >= 8, $"只有 {authors} 位作者");
    }

    [Fact]
    public void Picking_with_the_same_seed_gives_the_same_line()
    {
        var first = GameQuotes.Pick(new Random(20261110));
        var second = GameQuotes.Pick(new Random(20261110));
        Assert.Same(first, second);
        Assert.Contains(first, GameQuotes.All);
    }

    // 覆盖全集：随机数被固定成下标时，每一条都必须能被抽到。
    [Fact]
    public void Every_quote_can_be_picked()
    {
        for (var index = 0; index < GameQuotes.All.Count; index++)
            Assert.Same(GameQuotes.All[index], GameQuotes.Pick(new FixedRandom(index)));
    }

    sealed class FixedRandom(int value) : Random
    {
        public override int Next(int maxValue) => Math.Clamp(value, 0, maxValue - 1);
    }
}
