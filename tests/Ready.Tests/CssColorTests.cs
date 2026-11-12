using Ready.Services;
using Xunit;

namespace Ready.Tests;

/// <summary>
/// 这一层是原生窗口涂色的输入：app.js 只吐 hsl() 字符串，解析错了就会在状态栏上
/// 留下一条看不出来的色差，或者给深色底配上深色图标。
/// </summary>
public sealed class CssColorTests
{
    [Fact]
    public void Parses_the_surface_string_app_js_emits()
    {
        Assert.True(CssColor.TryParse("hsl(18 32% 98%)", out var r, out var g, out var b));
        // 浅色底三通道都接近白，且顺序是 r > g > b（珊瑚色相偏暖）。
        Assert.True(r > 245 && g > 245 && b > 235);
        Assert.True(r > b);
        Assert.False(CssColor.IsDark("hsl(18 32% 98%)"));
    }

    [Fact]
    public void Dark_surfaces_are_recognised()
    {
        Assert.True(CssColor.TryParse("hsl(272 14% 8%)", out var r, out var g, out var b));
        Assert.True(r < 40 && g < 40 && b < 40);
        Assert.True(CssColor.IsDark("hsl(272 14% 8%)"));
    }

    [Fact]
    public void Hsl_keeps_the_hue_it_was_given()
    {
        Assert.True(CssColor.TryParse("hsl(210 45% 40%)", out var r, out var g, out var b));
        Assert.True(b > r, "蓝色相的 surface 应当 b 最大");
    }

    [Theory]
    [InlineData("#FFFEFA", 255, 254, 250)]
    [InlineData("#fff", 255, 255, 255)]
    [InlineData("#000000", 0, 0, 0)]
    public void Parses_hex(string text, byte r, byte g, byte b)
    {
        Assert.True(CssColor.TryParse(text, out var actualR, out var actualG, out var actualB));
        Assert.Equal(r, actualR);
        Assert.Equal(g, actualG);
        Assert.Equal(b, actualB);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("red")]
    [InlineData("hsl(18)")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    public void Rejects_what_it_cannot_read(string? text)
    {
        Assert.False(CssColor.TryParse(text, out _, out _, out _));
        // 解析不了时按「浅色」处理，与 app.css 的首帧回退值一致。
        Assert.False(CssColor.IsDark(text));
    }
}
