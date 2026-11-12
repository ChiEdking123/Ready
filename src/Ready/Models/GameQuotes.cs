namespace Ready.Models;

/// <summary>一位知名游戏开发者的一句话。</summary>
/// <param name="Text">语录正文，中文。</param>
/// <param name="Author">姓名，中英并列，例如「宫本茂 Shigeru Miyamoto」。</param>
/// <param name="Source">出处：演讲、著作或身份。</param>
public sealed record GameQuote(string Text, string Author, string Source);

/// <summary>
/// 捕捉页首屏那条横幅的内容库。以前这里是两句固定的宣传语，现在改成随机一条开发者
/// 语录——同样占一行字，说的却是这门手艺的共识，而不是应用在自夸。
///
/// 语录集中放在这里而不是散进 UI：改文案只需动这一个文件，测试也能直接覆盖
/// （tests 工程通配编译 Models/*.cs，不需要改 csproj）。
///
/// 长度是有上限的：手机上横幅要压在三行以内，所以每条正文都控制在很短的篇幅；
/// GameQuotesTests 会守住这个上限，新增长句会在测试里立刻失败。
/// </summary>
public static class GameQuotes
{
    public static IReadOnlyList<GameQuote> All { get; } =
    [
        new("游戏是一连串有趣的决定。", "席德·梅尔 Sid Meier", "《文明》系列，GDC 2012"),
        new("最好的游戏都是易学难精的。", "诺兰·布什内尔 Nolan Bushnell", "雅达利创始人"),
        new("晚一点只是暂时的，烂是永久的。", "加布·纽维尔 Gabe Newell", "Valve 联合创始人"),
        new("名片上我是公司社长，脑子里我是游戏开发者，心里我是个玩家。", "岩田聪 Satoru Iwata", "GDC 2005 主题演讲"),
        new("延期的游戏最终会变好，赶工的游戏永远很糟。", "宫本茂 Shigeru Miyamoto", "常被归于任天堂内部的说法"),
        new("好点子不只解决一个问题，它能一次解决好几个。", "宫本茂 Shigeru Miyamoto", "流传于访谈，原始出处待考"),
        new("乐趣，不过是学习的另一个说法。", "拉夫·科斯特 Raph Koster", "《快乐之道》"),
        new("游戏是一个封闭的形式系统，主观地表现了现实的一部分。", "克里斯·克劳福德 Chris Crawford", "《计算机游戏设计艺术》1984"),
        new("游戏是一种带着玩心去解决问题的活动。", "杰西·谢尔 Jesse Schell", "《全景探秘游戏设计艺术》"),
        new("限制催生创造力。", "马克·罗斯沃特 Mark Rosewater", "万智牌首席设计师"),
        new("专注，就是决定哪些事你不去做。", "约翰·卡马克 John Carmack", "id Software 联合创始人"),
        new("用枯萎的技术做水平思考。", "横井军平 Gunpei Yokoi", "Game Boy 之父")
    ];

    /// <summary>抽一条。接受 <see cref="Random"/> 是为了让测试能固定结果。</summary>
    public static GameQuote Pick(Random random) => All[random.Next(All.Count)];

    /// <summary>抽一条，用进程共享的随机源。</summary>
    public static GameQuote Pick() => Pick(Random.Shared);
}
