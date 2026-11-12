using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Ready.Services;

namespace Ready;

/// <summary>
/// 进程入口。注册的服务和 MAUI 头一一对应：共享的 <see cref="IAppStore"/>、
/// 平台文件服务，以及 BlazorWebView 本身需要的宿主服务。
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // 平台壳的能力位。这个头自己拼 DI 容器、不走 MauiProgram，所以必须在这里单独
        // 赋值——漏了它，桌面版的「关闭窗口时」整段设置会凭空消失。
        NativeWindow.HasDesktopShell = true;
        NativeWindow.IsTouchFirst = false;
        NativeWindow.ExportUsesShareSheet = false;

        // 每显示器 DPI v2：跨屏拖动时由系统给出新的建议矩形，WebView2 也按新 DPI 重新栅格化。
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.ThreadException += (_, e) => ReadyPaths.AppendCrashLog(e.Exception);

        var services = new ServiceCollection();
        services.AddWindowsFormsBlazorWebView();
        services.AddSingleton<IAppStore>(_ => new AppStore(ReadyPaths.DataFile));
        services.AddSingleton<IPlatformFiles, WinFormsPlatformFiles>();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        using var provider = services.BuildServiceProvider();
        Application.Run(new MainForm(provider));
    }
}
