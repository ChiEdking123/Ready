using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Ready;

/// <summary>
/// 主窗口。它只做四件事：把 <see cref="BlazorWebView"/> 铺满客户区、按页面配色刷原生边框、
/// 托盘图标的显示/隐藏、以及关闭窗口时的三种策略。界面本身全在共享的 Blazor 组件里。
/// </summary>
internal sealed class MainForm : Form
{
    private const int DesignWidth = 1240;
    private const int DesignHeight = 860;

    private readonly BlazorWebView _blazor;
    private readonly NotifyIcon _tray;

    private WebView2? _webView;
    private string? _surface;
    private bool _dark;
    private bool _exiting;

    public MainForm(IServiceProvider services)
    {
        Text = "Ready! — 捕捉下一个好游戏";
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(900, 620);
        BackColor = Palette.LightSurface;

        var icon = LoadIcon();
        if (icon is not null) Icon = icon;

        _blazor = new BlazorWebView
        {
            Dock = DockStyle.Fill,
            HostPage = "wwwroot/index.html",
            Services = services,
            BackColor = Palette.LightSurface,
        };
        _blazor.RootComponents.Add<Components.Routes>("#app");
        _blazor.BlazorWebViewInitialized += OnBlazorInitialized;
        Controls.Add(_blazor);

        _tray = new NotifyIcon
        {
            Text = "Ready! — 捕捉下一个好游戏",
            Visible = true,
        };
        if (icon is not null) _tray.Icon = icon;

        var menu = new ContextMenuStrip();
        menu.Items.Add("显示窗口", null, (_, _) => ShowWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());
        _tray.ContextMenuStrip = menu;
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ToggleWindow();
        };

        // 页面里的「留到后台 / 彻底关闭」和原生关闭按钮走同一套实现。
        AppThemeBridge.SurfaceChanged += OnSurfaceChanged;
        AppThemeBridge.HideToTrayHandler = HideToTray;
        AppThemeBridge.ExitHandler = ExitApplication;
        FormClosing += OnFormClosing;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        PlaceOnNearestScreen();
        ApplyPalette(_dark, _surface);
    }

    /// <summary>
    /// 初值尺寸按「逻辑像素」给，实际落地时乘上当前显示器的缩放比：
    /// 1240×860 在 100% 下就是这个像素数，在 150% 下窗口的物理尺寸看起来一致。
    /// </summary>
    private void PlaceOnNearestScreen()
    {
        var screen = Screen.FromPoint(Cursor.Position) ?? Screen.PrimaryScreen;
        if (screen is null) return;

        var work = screen.WorkingArea;
        var scale = DeviceDpi / 96.0;
        if (scale <= 0) scale = 1;

        var width = Math.Min((int)Math.Round(DesignWidth * scale), work.Width);
        var height = Math.Min((int)Math.Round(DesignHeight * scale), work.Height);

        Bounds = new Rectangle(
            work.X + ((work.Width - width) / 2),
            work.Y + ((work.Height - height) / 2),
            width,
            height);
    }

    private void OnBlazorInitialized(object? sender, BlazorWebViewInitializedEventArgs args)
    {
        if (args.WebView is not WebView2 view) return;
        _webView = view;

        // 页面里的 blazor.webview.js 带 autostart="false"：Android 的 BlazorWebView 要先注入
        // window.external 桥再由宿主启动，脚本不能抢跑。代价是页面自己不再自启，而 WinForms
        // 这边两个宿主谁都不替它调 Blazor.start()——少了下面这一步，页面会永远停在空壳上，
        // 只剩 body 的底色，既没内容也没有任何异常。
        if (view.CoreWebView2 is { } core)
        {
            core.NavigationCompleted += OnNavigationCompleted;
        }

        try
        {
            // 页面自己有 Ctrl + 滚轮缩放（改根字号），WebView 的内建缩放会与之打架。
            view.CoreWebView2.Settings.IsZoomControlEnabled = false;
            view.CoreWebView2.Settings.IsPinchZoomEnabled = false;
        }
        catch (Exception)
        {
            // 旧运行时可能不暴露全部设置，页面不依赖它也能用。
        }

        ApplyPalette(_dark, _surface);
    }

    /// <summary>
    /// 导航完成即启动 Blazor。导航完成时 blazor.webview.js 已经执行过，window.Blazor 存在；
    /// 但这只保证「能启动」，不保证「没启动过」——页面若自行启动了，第二次调用会抛
    /// 「Blazor has already started」，那属于预期内的重复，不影响使用。
    /// </summary>
    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || sender is not CoreWebView2 core) return;

        try
        {
            await core.ExecuteScriptAsync("window.Blazor && window.Blazor.start();");
        }
        catch (Exception)
        {
            // 启动失败只会让页面停在空壳上，不该把宿主一起带走。
        }
    }

    private void OnSurfaceChanged(string color) => ApplyPalette(AppThemeBridge.IsDark(color), color);

    private void ApplyPalette(bool dark, string? surface)
    {
        _dark = dark;
        _surface = surface;

        var color = Palette.Surface(surface, dark);
        BackColor = color;
        _blazor.BackColor = color;
        Palette.ApplyTitleBar(Handle, dark, color);

        try
        {
            if (_webView?.CoreWebView2 is { } core)
            {
                // 原生滚动条也跟着走，否则深色页面配一条亮滚动条。
                core.Profile.PreferredColorScheme = dark
                    ? CoreWebView2PreferredColorScheme.Dark
                    : CoreWebView2PreferredColorScheme.Light;
            }
        }
        catch (Exception)
        {
            // 纯外观。
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_exiting) return;

        var behavior = CloseChoice.Load();
        if (behavior == CloseBehavior.Exit)
        {
            _exiting = true;
            DisposeTray();
            return;
        }

        if (behavior == CloseBehavior.Tray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        // 第一次关闭：让页面来问，因为它还能提醒未保存的内容。
        e.Cancel = true;
        if (!AppThemeBridge.HasCloseListener)
        {
            AskOnFirstClose();
            return;
        }

        AppThemeBridge.RaiseCloseRequested();
    }

    private void AskOnFirstClose()
    {
        // 这个对话框放不下「下次不再询问」，所以不能替用户记住选择：答案只作用于本次，
        // 记住选择只有两条路——页面里的对话框（带复选框）或设置页。
        var behavior = ClosePrompt.Ask(this);

        if (behavior == CloseBehavior.Tray) HideToTray();
        else ExitApplication();
    }

    private void ToggleWindow()
    {
        if (Visible) HideToTray();
        else ShowWindow();
    }

    private void ShowWindow()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    private void HideToTray() => Hide();

    private void ExitApplication()
    {
        _exiting = true;
        DisposeTray();
        Application.Exit();
    }

    private void DisposeTray()
    {
        _tray.Visible = false;
        _tray.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AppThemeBridge.SurfaceChanged -= OnSurfaceChanged;
            AppThemeBridge.HideToTrayHandler = null;
            AppThemeBridge.ExitHandler = null;
            _tray.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Icon? LoadIcon()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appicon.ico");
            if (File.Exists(path)) return new Icon(path);
        }
        catch (Exception)
        {
            // 图标读不出来只是难看，不能让应用起不来。
        }

        return SystemIcons.Application;
    }
}
