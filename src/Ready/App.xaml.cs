namespace Ready;

public partial class App : Application
{
    private static bool _pageDark;

    /// <summary>Raised when the effective color scheme changes, so native chrome can follow.</summary>
    public static event Action<bool>? NativeAppearanceChanged;

    /// <summary>
    /// True when the page is rendered on a dark surface. The page is the single source of
    /// truth: it resolves "system" against the OS preference and reports the resulting
    /// surface color back, so the native window never guesses a different answer.
    /// </summary>
    public static bool PrefersDark() => _pageDark;

    public App()
    {
        InitializeComponent();

        Ready.AppThemeBridge.SurfaceChanged += OnSurfaceChanged;
        RequestedThemeChanged += (_, _) => NotifyAppearance();
    }

    private static void OnSurfaceChanged(string color)
    {
        _pageDark = Ready.AppThemeBridge.IsDark(color);
        NotifyAppearance();
    }

    private static void NotifyAppearance() => NativeAppearanceChanged?.Invoke(_pageDark);

    // 手机独占窗口，没有初始尺寸可设：窗口始终铺满屏幕。
    // Windows 端的尺寸由 src/Ready.WinForms/MainForm.PlaceOnNearestScreen 负责。
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new MainPage()) { Title = "Ready! — 捕捉下一个好游戏" };
}
