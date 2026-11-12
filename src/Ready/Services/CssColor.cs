using System.Globalization;

namespace Ready.Services;

/// <summary>
/// Parses the CSS colors the page writes (the Material ramp is emitted as
/// <c>hsl(h s% l%)</c>, plus plain <c>#rrggbb</c> / <c>#rgb</c>) into 8-bit channels so
/// native windows can paint themselves.
///
/// Deliberately free of platform types: <c>Services/**</c> is compiled directly into
/// <c>Ready.WinForms</c> and the test project, neither of which references MAUI.
/// </summary>
public static class CssColor
{
    public static bool TryParse(string? text, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var value = text.Trim();
        if (value.StartsWith("hsl", StringComparison.OrdinalIgnoreCase)) return TryParseHsl(value, out r, out g, out b);
        if (value.StartsWith('#')) return TryParseHex(value, out r, out g, out b);
        return false;
    }

    /// <summary>
    /// True when white text would be the better foreground. Unknown colors answer
    /// false, matching the "assume light surface" default of the fallback palette.
    /// </summary>
    public static bool IsDark(string? text) =>
        TryParse(text, out var r, out var g, out var b) && 0.299 * r + 0.587 * g + 0.114 * b < 128;

    private static bool TryParseHsl(string value, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;

        var open = value.IndexOf('(');
        var close = value.LastIndexOf(')');
        if (open < 0 || close <= open) return false;

        var parts = value[(open + 1)..close].Split([' ', ',', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var hue)) return false;
        if (!double.TryParse(parts[1].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var saturation)) return false;
        if (!double.TryParse(parts[2].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var lightness)) return false;

        (r, g, b) = FromHsl(hue, saturation / 100.0, lightness / 100.0);
        return true;
    }

    private static (byte R, byte G, byte B) FromHsl(double hue, double saturation, double lightness)
    {
        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var h = ((hue % 360) + 360) % 360 / 60.0;
        var x = c * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = h switch
        {
            < 1 => (c, x, 0.0),
            < 2 => (x, c, 0.0),
            < 3 => (0.0, c, x),
            < 4 => (0.0, x, c),
            < 5 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };
        var m = lightness - c / 2;
        return (Channel(r + m), Channel(g + m), Channel(b + m));
    }

    private static bool TryParseHex(string value, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;

        var hex = value[1..];
        if (hex.Length == 3) hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
        if (hex.Length != 6) return false;

        try
        {
            r = byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);
}
