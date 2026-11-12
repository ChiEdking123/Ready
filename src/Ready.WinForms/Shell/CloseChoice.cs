using System.IO;
using System.Text.Json;

namespace Ready;

/// <summary>
/// 原生层要读「关闭窗口时怎么办」，而这一步发生在 Blazor 起来之前（甚至页面还没加载时），
/// 所以不能依赖 <see cref="IAppStore"/>，直接读同一个数据文件里的 Settings.CloseBehavior。
///
/// 只读不写：想改这个值，唯一的路径是设置页（或带复选框的关闭对话框），它们都经过
/// <see cref="IAppStore"/> 落盘。早先这里还有一个 Save()，但没有任何调用方——页面自己
/// 会写，留两条写入路径只会让「谁最后写的」变成运气问题。
/// </summary>
internal static class CloseChoice
{
    public static string Load()
    {
        try
        {
            var path = ReadyPaths.DataFile;
            if (!File.Exists(path)) return CloseBehavior.Ask;

            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("Settings", out var settings) &&
                settings.TryGetProperty("CloseBehavior", out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return CloseBehavior.Normalize(value.GetString());
            }
        }
        catch (Exception)
        {
            // 文件坏了不能挡住关闭流程，退回「问一次」。
        }

        return CloseBehavior.Ask;
    }
}
