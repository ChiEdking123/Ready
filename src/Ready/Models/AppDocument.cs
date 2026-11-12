namespace Ready.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "light";
    public string Accent { get; set; } = "coral";
    public bool ReduceMotion { get; set; }
    public bool CompactMode { get; set; }
    public bool AutosaveDraft { get; set; } = true;
    public bool ConfirmDelete { get; set; } = true;

    /// <summary>Interface scale in percent. Snapped to <see cref="UiScale.Steps"/> on load.</summary>
    public int UiScalePercent { get; set; } = 100;

    /// <summary>What the window close button does. See <see cref="Ready.Models.CloseBehavior"/>.</summary>
    public string CloseBehavior { get; set; } = Ready.Models.CloseBehavior.Ask;

    public void Normalize()
    {
        UiScalePercent = UiScale.Nearest(UiScalePercent);
        CloseBehavior = Ready.Models.CloseBehavior.Normalize(CloseBehavior);
    }
}

/// <summary>What happens when the user closes the window.</summary>
public static class CloseBehavior
{
    /// <summary>Every time: ask whether to keep running in the tray.</summary>
    public const string Ask = "ask";

    /// <summary>Hide to the notification area and keep the process alive.</summary>
    public const string Tray = "tray";

    /// <summary>Quit immediately.</summary>
    public const string Exit = "exit";

    public static string Normalize(string? value) => value switch
    {
        Tray => Tray,
        Exit => Exit,
        _ => Ask
    };
}

/// <summary>The interface scale ladder offered by the settings slider.</summary>
public static class UiScale
{
    public static readonly int[] Steps = [85, 100, 112, 125, 140];

    public const int Default = 100;

    /// <summary>Index of the nearest step, so a stored value always lands on the slider rail.</summary>
    public static int NearestIndex(int percent)
    {
        var best = 0;
        for (var i = 1; i < Steps.Length; i++)
            if (Math.Abs(Steps[i] - percent) < Math.Abs(Steps[best] - percent)) best = i;
        return best;
    }

    public static int Nearest(int percent) => Steps[NearestIndex(percent)];

    /// <summary>Move one step up (+1) or down (-1), clamped to the ends of the ladder.</summary>
    public static int Nudge(int percent, int direction)
    {
        var index = NearestIndex(percent);
        if (direction == 0) return Steps[index];
        return Steps[Math.Clamp(index + Math.Sign(direction), 0, Steps.Length - 1)];
    }
}

public sealed class AppDocument
{
    /// <summary>2 起「关闭窗口时」只有在用户显式勾选后才写入；见 <c>AppStore.MigrateClosePrompt</c>。</summary>
    public int SchemaVersion { get; set; } = 2;
    public List<IdeaRecord> Ideas { get; set; } = [];
    public IdeaDraft Draft { get; set; } = new();
    public Dictionary<string, List<string>> CustomTags { get; set; } = [];
    public AppSettings Settings { get; set; } = new();
}

public sealed class IdeaExport
{
    public string App { get; set; } = "Ready!";
    public int Version { get; set; } = 1;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.Now;
    public List<IdeaRecord> Ideas { get; set; } = [];
}

public static class TagCatalog
{
    private static readonly Dictionary<string, string[]> Defaults = new()
    {
        ["genres"] = ["动作", "冒险", "角色扮演", "策略", "模拟经营", "解谜", "平台跳跃", "Roguelike", "生存", "卡牌", "射击", "音乐节奏", "派对社交", "文字叙事", "放置"],
        ["perspectives"] = ["第一人称", "第三人称", "俯视角", "等距视角", "横版", "2D 固定", "3D 自由"],
        ["art"] = ["像素", "低多边形", "手绘", "卡通渲染", "写实", "极简", "复古", "黑白", "黏土", "剪纸"],
        ["settings"] = ["现代都市", "奇幻", "科幻", "赛博朋克", "废土", "历史", "校园", "日常", "童话", "恐怖", "架空世界"],
        ["roles"] = ["成年男性", "成年女性", "少女", "少年", "儿童", "老人", "机器人", "拟人角色", "动物", "怪物", "非人存在", "群像"],
        ["platforms"] = ["Windows", "macOS", "Linux", "Android", "iOS", "Web", "Switch", "PlayStation", "Xbox", "VR"],
        ["audiences"] = ["休闲型玩家", "成就型玩家", "探索型玩家", "社交型玩家", "沉浸型玩家"],
        ["durations"] = ["15 分钟以内", "15–60 分钟", "1–3 小时", "3–8 小时", "8–20 小时", "20+ 小时", "持续游玩"],
        ["loops"] = ["15–60 秒", "1–3 分钟", "3–10 分钟", "10–30 分钟", "30+ 分钟"],
        ["emotions"] = ["放松", "快乐", "惊喜", "好奇", "紧张", "挑战", "成就感", "共鸣", "感动", "怀旧", "恐惧", "心流"],
        ["inputs"] = ["键鼠", "触屏", "手柄", "体感", "VR 控制器", "语音"],
        ["resources"] = ["程序", "美术", "动画", "关卡", "叙事", "音频", "UI/UX", "系统策划", "测试", "运营"],
        ["nongoals"] = ["不做多人联机", "不做 PvP", "不做开放世界", "不做长线剧情", "不做多结局", "不做 3D", "不做内购", "不做账号系统", "不做赛季运营"],
        ["monetization"] = ["买断制", "免费游玩", "免费 + 内购", "订阅制", "广告支持", "一次性 DLC", "公益 / 展示"]
    };

    public static readonly string[] Scales = ["实验性质", "中小型", "商业级", "3A"];
    public static IReadOnlyList<string> All(string field, IReadOnlyDictionary<string, List<string>> custom) =>
        Defaults.TryGetValue(field, out var tags)
            ? custom.TryGetValue(field, out var extra) ? [.. tags.Concat(extra)] : tags
            : custom.TryGetValue(field, out extra) ? extra : [];
}
