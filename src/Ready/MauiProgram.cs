using Microsoft.Extensions.Logging;

namespace Ready;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp() => CreateMauiAppBuilder().Build();

    /// <summary>
    /// The Windows head needs the builder rather than the finished app, so it can add
    /// lifecycle hooks (window creation for the tray icon and frame colors).
    /// </summary>
    public static MauiAppBuilder CreateMauiAppBuilder()
    {
        ConfigureShell();

        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<IAppStore>(_ => new AppStore(Path.Combine(FileSystem.AppDataDirectory, "ready-data.json")));
        builder.Services.AddSingleton<IPlatformFiles, MauiPlatformFiles>();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
        return builder;
    }

    /// <summary>
    /// 平台壳的能力位，必须在任何 Blazor 组件渲染之前写死：界面靠它们决定显示不显示
    /// 托盘相关的设置。这里用编译常量而不是 DeviceInfo，因为 Services/** 也会被不引用
    /// MAUI 的 WinForms 头和测试项目编译。
    /// </summary>
    private static void ConfigureShell()
    {
#if ANDROID
        NativeWindow.HasDesktopShell = false;
        NativeWindow.IsTouchFirst = true;
        NativeWindow.ExportUsesShareSheet = true;
#else
        NativeWindow.HasDesktopShell = true;
        NativeWindow.IsTouchFirst = false;
        NativeWindow.ExportUsesShareSheet = false;
#endif
    }
}
