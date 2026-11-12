using Microsoft.AspNetCore.Components.WebView;

namespace Ready;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

        // .NET 10 的 ContentPage 默认是边到边（SafeAreaEdges=None），顶栏会被状态栏压住、
        // 底栏会被手势条压住。让原生把 WebView 缩进安全区，页面里的 height:100% 链路
        // 就不用改，也不会出现内外两层滚动。
        //
        // 30 起 decorFitsSystemWindows=false 后 adjustResize 失效，键盘必须由 SoftInput
        // 让位；29 及以下 adjustResize 仍然会缩窗口，再叠一层键盘 inset 就是双重让位。
        SafeAreaEdges = OperatingSystem.IsAndroidVersionAtLeast(30)
            ? Microsoft.Maui.SafeAreaEdges.All
            : new Microsoft.Maui.SafeAreaEdges(Microsoft.Maui.SafeAreaRegions.Container);

        blazorWebView.BlazorWebViewInitialized += OnAndroidWebViewInitialized;
    }

    // 画面比例是唯一的缩放入口。meta viewport 里的 user-scalable=no 只是声明，
    // Chromium 在不同版本上对它的处理并不一致，真正的闸门在这里。
    private void OnAndroidWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs args)
    {
        if (args.WebView is not Android.Webkit.WebView view) return;

        if (view.Settings is { } settings)
        {
            settings.SetSupportZoom(false);
            settings.BuiltInZoomControls = false;
            settings.DisplayZoomControls = false;
            // 刻意不动 TextZoom：那是系统的字体缩放，用户调大字号往往有真实需求。
        }

        // WebView 默认白底，深色模式下首帧会闪一下。
        view.SetBackgroundColor(new Android.Graphics.Color(AndroidChrome.CurrentBackground));
    }
}
