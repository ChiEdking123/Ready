using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Activity;
using AndroidX.Core.View;

namespace Ready;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true,
          // 键盘只允许把窗口压缩。adjustPan 会把整页顶上去，而提交按钮在顶栏，
          // 一顶就点不到了。
          WindowSoftInputMode = SoftInput.AdjustResize,
          ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private BackToExit? _back;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // 边到边：页面自己按安全区让位（MainPage.SafeAreaEdges），状态栏与导航栏
        // 变成透明的浮层，透出的是 AndroidChrome 涂的窗口底色。
        if (Window is not null) WindowCompat.SetDecorFitsSystemWindows(Window, false);

        AndroidChrome.Attach(this);
        App.NativeAppearanceChanged += AndroidChrome.OnAppearanceChanged;
        AppThemeBridge.SurfaceChanged += AndroidChrome.OnSurfaceChanged;

        // 冷启动的头几帧 app.js 还没跑，先按系统夜间模式涂一次，避免白底闪一下。
        AndroidChrome.OnAppearanceChanged(App.PrefersDark());

        _back = new BackToExit(this);
        OnBackPressedDispatcher.AddCallback(this, _back);

        // 页面上的「彻底关闭」在这里落成「关掉整个任务」，与 Windows 的退出同义。
        AppThemeBridge.ExitHandler = FinishAffinity;
    }

    protected override void OnDestroy()
    {
        App.NativeAppearanceChanged -= AndroidChrome.OnAppearanceChanged;
        AppThemeBridge.SurfaceChanged -= AndroidChrome.OnSurfaceChanged;
        // 静态字段不能攥着一个已经销毁的 Activity。
        AppThemeBridge.ExitHandler = null;
        _back?.Remove();
        _back = null;
        AndroidChrome.Detach();
        base.OnDestroy();
    }

    /// <summary>
    /// 系统返回键 / 返回手势。优先级：收起键盘 → 交给页面决定 → 页面还没就绪就别杀进程。
    /// </summary>
    private sealed class BackToExit : OnBackPressedCallback
    {
        private readonly MainActivity _activity;

        public BackToExit(MainActivity activity) : base(true) => _activity = activity;

        public override void HandleOnBackPressed()
        {
            if (_activity.Window is { } window && window.DecorView is { } decor
                && ViewCompat.GetRootWindowInsets(decor) is { } insets
                && insets.IsVisible(WindowInsetsCompat.Type.Ime()))
            {
                new WindowInsetsControllerCompat(window, decor).Hide(WindowInsetsCompat.Type.Ime());
                return;
            }

            if (!AppThemeBridge.HasBackListener)
            {
                // 页面还没渲染出来，此时没有任何界面能告诉用户「已退出」，
                // 退到后台等它画完，比直接结束进程合适。
                _activity.MoveTaskToBack(true);
                return;
            }

            NativeWindow.RequestBack();
        }
    }
}
