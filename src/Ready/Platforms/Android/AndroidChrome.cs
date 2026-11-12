using Android.App;
using Android.Content.Res;
using Android.Graphics.Drawables;
using AndroidX.Core.View;
using Microsoft.Maui.ApplicationModel;
using Ready.Services;
using AndroidColor = Android.Graphics.Color;

namespace Ready;

/// <summary>
/// Android 侧的窗口外观：窗口底色、系统栏的透明度与图标明暗。
///
/// 配色的唯一来源始终是页面——app.js 把整条 Material 色阶算出来后，把 surface 颜色
/// 回传给原生，这里只负责把它涂上去。页面还没报过颜色时（冷启动的头几帧），按系统
/// 夜间模式先挑一个近似底色，等页面报上来再校正。
/// </summary>
internal static class AndroidChrome
{
    // 与 app.css 里 :root 的首帧回退值保持一致，避免「启动屏 → 首帧」出现跳色。
    private const string LightSurface = "#FFFEFA";
    private const string DarkSurface = "#1A1210";

    private static Activity? _activity;
    private static string? _surface;

    /// <summary>WebView 首帧的底色：WebView 默认是白的，深色模式下会闪一下。</summary>
    public static int CurrentBackground => Resolve(_surface);

    public static void Attach(Activity activity)
    {
        _activity = activity;
        Apply(_surface);
    }

    public static void Detach()
    {
        _activity = null;
        _surface = null;
    }

    /// <summary>The page reported a new surface color.</summary>
    public static void OnSurfaceChanged(string color)
    {
        _surface = color;
        MainThread.BeginInvokeOnMainThread(() => Apply(color));
    }

    /// <summary>The page flipped between light and dark (system preference changed).</summary>
    public static void OnAppearanceChanged(bool _) =>
        MainThread.BeginInvokeOnMainThread(() => Apply(_surface));

    private static void Apply(string? surface)
    {
        if (_activity?.Window is not { } window) return;

        var background = Resolve(surface);
        window.SetBackgroundDrawable(new ColorDrawable(new AndroidColor(background)));
        window.DecorView?.SetBackgroundColor(new AndroidColor(background));

        // 边到边之后状态栏本身就是一块透明区域，透出来的是窗口底色。API 35 起
        // SetStatusBarColor 已废弃且无效，只在旧版本上显式透明一次，免得厂商主题
        // 给状态栏糊一层不透明色。
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            window.SetStatusBarColor(AndroidColor.Transparent);
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                window.SetNavigationBarColor(AndroidColor.Transparent);
            }
            else
            {
                // API 26 以下没有深色导航栏图标，透出浅色底会让三个键看不见。
                window.SetNavigationBarColor(AndroidColor.ParseColor(DarkSurface));
            }
        }

        var dark = IsDark(background);
        var decor = window.DecorView;
        if (decor is not null)
        {
            var controller = new WindowInsetsControllerCompat(window, decor);
            controller.AppearanceLightStatusBars = !dark;
            // API 26 以下无效，上面的深底配色兜住可读性。
            controller.AppearanceLightNavigationBars = !dark;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            // 否则浅色导航栏会被系统盖一层灰纱，和页面底色对不上。
            window.NavigationBarContrastEnforced = false;
        }
    }

    private static int Resolve(string? surface)
    {
        if (CssColor.TryParse(surface, out var r, out var g, out var b)) return AndroidColor.Rgb(r, g, b).ToArgb();

        var night = _activity?.Resources?.Configuration?.UiMode & UiMode.NightMask;
        return AndroidColor.ParseColor(night == UiMode.NightYes ? DarkSurface : LightSurface).ToArgb();
    }

    private static bool IsDark(int argb)
    {
        var r = (argb >> 16) & 0xFF;
        var g = (argb >> 8) & 0xFF;
        var b = argb & 0xFF;
        return 0.299 * r + 0.587 * g + 0.114 * b < 128;
    }
}
