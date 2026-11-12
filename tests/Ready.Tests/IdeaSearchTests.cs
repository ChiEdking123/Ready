using Ready.Models;
using Ready.Services;
using Xunit;

namespace Ready.Tests;

public sealed class IdeaSearchTests
{
    static IdeaRecord Sample()
    {
        var idea = new IdeaRecord
        {
            Summary = "在时间里经营深夜食堂",
            References = "《星露谷物语》",
            Scale = "中小型"
        };
        idea.Genres.AddRange(["模拟经营", "叙事"]);
        idea.Roles.AddRange(["猫咪店主", "夜班客人"]);
        idea.NonGoals.Add("不做多人联机");
        return idea;
    }

    [Fact]
    public void Text_covers_summary_references_scale_and_every_tag_field()
    {
        var text = IdeaSearch.Text(Sample());

        Assert.Contains("深夜食堂", text);
        Assert.Contains("星露谷物语", text);
        Assert.Contains("中小型", text);
        Assert.Contains("模拟经营", text);
        Assert.Contains("夜班客人", text);
        // 执行规划里的字段同样要参与搜索，否则「不做什么」这种边界写在库里搜不到。
        Assert.Contains("不做多人联机", text);
    }

    [Fact]
    public void Text_uses_the_same_tag_fields_as_persistence()
    {
        // TagFields 是保存与搜索共用的唯一一份清单；漏一个字段就意味着「存下来了却搜不到」。
        var idea = new IdeaRecord { Summary = "字段清单" };
        foreach (var field in IdeaData.TagFields) idea.TagsFor(field)!.Add("标记-" + field);

        var text = IdeaSearch.Text(idea);
        foreach (var field in IdeaData.TagFields) Assert.Contains("标记-" + field, text);
    }

    [Theory]
    [InlineData("食堂")]
    [InlineData("星露谷")]
    [InlineData("中小型")]
    [InlineData("猫咪")]
    [InlineData("多人联机")]
    public void Matches_finds_any_indexed_field(string query)
    {
        Assert.True(IdeaSearch.Matches(IdeaSearch.Text(Sample()), query));
    }

    [Theory]
    [InlineData("STARDEW")]
    [InlineData("stardew")]
    [InlineData("  猫咪  ")]
    [InlineData("  食堂  ")]
    public void Matches_is_trimmed_and_case_insensitive(string query)
    {
        var text = "Stardew 猫咪 食堂 summary";
        Assert.True(IdeaSearch.Matches(text, query));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_query_matches_everything(string? query)
    {
        Assert.True(IdeaSearch.Matches("anything", query));
    }

    [Fact]
    public void Unrelated_word_does_not_match()
    {
        Assert.False(IdeaSearch.Matches(IdeaSearch.Text(Sample()), "太空歌剧"));
    }

    [Fact]
    public void Custom_tags_are_searchable_because_they_live_in_the_same_lists()
    {
        var idea = Sample();
        idea.Genres.Add("治愈模拟");
        Assert.True(IdeaSearch.Matches(IdeaSearch.Text(idea), "治愈模拟"));
    }
}
