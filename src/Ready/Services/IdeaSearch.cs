using Ready.Models;

namespace Ready.Services;

/// <summary>
/// 构思库搜索用的纯文本投影。
///
/// 这段拼接以前写在 LibraryView 的渲染路径里：每敲一个字，所有构思的全部标签都要
/// 重新 string.Join 一遍，再逐条 Contains。构思一多，输入就会顶着主线程。
/// 提成独立类型之后，组件可以按 Id 缓存结果，只在数据真的变了时重建；
/// 测试工程也能直接覆盖它，而不用去渲染组件。
/// </summary>
public static class IdeaSearch
{
    /// <summary>
    /// 参与搜索的字段与顺序保持原样：一句话总结 → 参考作品 → 规模 → 全部标签。
    /// 顺序不影响命中，但固定下来能让缓存与调试时的输出可预期。
    /// </summary>
    public static string Text(IdeaRecord idea) =>
        string.Join(' ', [idea.Summary, idea.References, idea.Scale,
            .. IdeaData.TagFields.SelectMany(field => idea.TagsFor(field) ?? [])]);

    /// <summary>空查询命中一切；否则在预拼好的文本里做大小写不敏感的子串匹配。</summary>
    public static bool Matches(string text, string? query)
    {
        var term = query?.Trim();
        return string.IsNullOrEmpty(term) || text.Contains(term, StringComparison.OrdinalIgnoreCase);
    }
}
