using System.Windows.Forms;

namespace Ready;

/// <summary>
/// 网页还没接管之前（首帧之前）用户就点了关闭时的兜底提示。
/// 页面一旦订阅了关闭请求，就改用页面里的弹窗，因为那里能提示未保存的内容。
/// </summary>
internal static class ClosePrompt
{
    /// <summary>返回用户选择：<see cref="CloseBehavior.Tray"/> 或 <see cref="CloseBehavior.Exit"/>。</summary>
    public static string Ask(IWin32Window owner)
    {
        var answer = MessageBox.Show(
            owner,
            "选「是」留到托盘：Ready! 继续在后台待命，点托盘图标随时回来。\n" +
            "选「否」彻底退出：关闭所有窗口并结束进程。\n\n" +
            "以后可以在「设置 → 关闭窗口时」修改这个选择。",
            "要留到托盘，还是彻底退出？",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);

        return answer == DialogResult.Yes ? CloseBehavior.Tray : CloseBehavior.Exit;
    }
}
