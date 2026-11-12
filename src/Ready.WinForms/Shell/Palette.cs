using System.Drawing;
using System.Runtime.InteropServices;

namespace Ready;

/// <summary>
/// 配色从页面来，窗口和系统标题栏跟着走。app.js 统一用 hsl() 表达主题，
/// 这里把它换算成 GDI 颜色，再通过 DWM 涂到原生边框上。
/// </summary>
internal static class Palette
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmUseImmersiveDarkModeLegacy = 19;
    private const int DwmCaptionColor = 35;
    private const int DwmTextColor = 36;

    public static Color LightSurface { get; } = Color.FromArgb(0xFF, 0xF8, 0xF6);

    public static Color DarkSurface { get; } = Color.FromArgb(0x1A, 0x12, 0x10);

    public static Color Foreground(bool dark) => dark
        ? Color.FromArgb(0xF1, 0xDF, 0xDA)
        : Color.FromArgb(0x22, 0x1A, 0x18);

    /// <summary>解析失败或页面还没上报时，退回与页面一致的默认底色。</summary>
    public static Color Surface(string? css, bool dark)
    {
        var parsed = FromCss(css);
        return parsed ?? (dark ? DarkSurface : LightSurface);
    }

    public static void ApplyTitleBar(IntPtr handle, bool dark, Color surface)
    {
        if (handle == IntPtr.Zero) return;

        var useDark = dark ? 1 : 0;
        SetAttribute(handle, DwmUseImmersiveDarkMode, useDark);
        SetAttribute(handle, DwmUseImmersiveDarkModeLegacy, useDark);

        // Windows 11 起可以直接给标题栏上色；更老的系统会忽略这两条。
        SetAttribute(handle, DwmCaptionColor, ToColorRef(surface));
        SetAttribute(handle, DwmTextColor, ToColorRef(Foreground(dark)));
    }

    private static Color? FromCss(string? css)
    {
        if (string.IsNullOrWhiteSpace(css)) return null;
        var value = css.Trim();
        if (!value.StartsWith("hsl", StringComparison.OrdinalIgnoreCase)) return null;

        var open = value.IndexOf('(');
        var close = value.LastIndexOf(')');
        if (open < 0 || close <= open) return null;

        var parts = value[(open + 1)..close].Split([' ', ',', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return null;

        if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hue)) return null;
        if (!double.TryParse(parts[1].TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var saturation)) return null;
        if (!double.TryParse(parts[2].TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lightness)) return null;

        return FromHsl(hue, saturation / 100.0, lightness / 100.0);
    }

    private static Color FromHsl(double hue, double saturation, double lightness)
    {
        var chroma = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var h = ((hue % 360) + 360) % 360 / 60.0;
        var x = chroma * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = h switch
        {
            < 1 => (chroma, x, 0.0),
            < 2 => (x, chroma, 0.0),
            < 3 => (0.0, chroma, x),
            < 4 => (0.0, x, chroma),
            < 5 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x)
        };
        var m = lightness - chroma / 2;
        return Color.FromArgb(Channel(r + m), Channel(g + m), Channel(b + m));
    }

    private static int Channel(double value) => (int)Math.Clamp(Math.Round(value * 255), 0, 255);

    /// <summary>DWM 要的是 COLORREF：0x00BBGGRR，注意和 ARGB 的字节序相反。</summary>
    private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

    private static void SetAttribute(IntPtr handle, int attribute, int value)
    {
        try { DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int)); }
        catch (Exception) { /* 老系统不支持某个属性，跳过即可。 */ }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
