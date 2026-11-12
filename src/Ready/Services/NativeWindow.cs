namespace Ready;

/// <summary>
/// The bridge between the Blazor layer and the native window. The page owns the palette,
/// so it reports the resulting surface color instead of the platform guessing; the window
/// then paints its frame, title bar and background to match. On platforms without a tray
/// icon the handlers stay unset and every call is a quiet no-op.
/// </summary>
public static class NativeWindow
{
    // ---- 平台壳的能力位 ----
    // 进程启动时由各个头一次性写入，之后全程只读：它们描述的是「这个宿主有没有这种
    // 能力」，不是运行期状态。默认全部 false，等于「什么都不提供」——万一某个头忘了
    // 赋值，界面只会少显示一块，不会出现「按钮在、点了没用」的死控件。
    /// <summary>有系统托盘与关闭询问（Windows 的两个头）。</summary>
    public static bool HasDesktopShell { get; set; }

    /// <summary>主要输入方式是触摸：隐藏 Ctrl / 滚轮一类的提示。</summary>
    public static bool IsTouchFirst { get; set; }

    /// <summary>导出走系统分享面板，而不是「另存为」。</summary>
    public static bool ExportUsesShareSheet { get; set; }

    public static void ApplySurface(string color) => AppThemeBridge.RaiseSurfaceChanged(color);

    public static void HideToTray() => AppThemeBridge.RaiseHideToTray();

    public static void Exit() => AppThemeBridge.RaiseExit();

    /// <summary>系统返回键 / 返回手势到达页面。</summary>
    public static void RequestBack() => AppThemeBridge.RaiseBackRequested();
}

/// <summary>
/// Events and hooks shared by the shared UI and the Windows head. Kept free of any
/// Windows type so the same file compiles for Android.
/// </summary>
public static class AppThemeBridge
{
    public static event Action<string>? SurfaceChanged;

    public static event Action? CloseRequested;

    /// <summary>Raised by the Android head for the system back button and back gesture.</summary>
    public static event Action? BackRequested;

    /// <summary>Set by the Windows head so "留在后台" can actually hide the window.</summary>
    public static Action? HideToTrayHandler { get; set; }

    /// <summary>Set by the Windows head so "彻底关闭" can end the process.</summary>
    public static Action? ExitHandler { get; set; }

    public static void RaiseSurfaceChanged(string color)
    {
        if (string.IsNullOrWhiteSpace(color)) return;
        SurfaceChanged?.Invoke(color);
    }

    public static bool HasCloseListener => CloseRequested is not null;

    /// <summary>False until the page has subscribed, i.e. before its first render.</summary>
    public static bool HasBackListener => BackRequested is not null;

    public static void RaiseCloseRequested() => CloseRequested?.Invoke();

    public static void RaiseBackRequested() => BackRequested?.Invoke();

    public static void RaiseHideToTray() => HideToTrayHandler?.Invoke();

    public static void RaiseExit() => ExitHandler?.Invoke();

    /// <summary>
    /// True when the surface the page reported is dark, so window chrome knows which
    /// way to paint its bars and title text.
    /// </summary>
    public static bool IsDark(string? color) => Services.CssColor.IsDark(color);
}
