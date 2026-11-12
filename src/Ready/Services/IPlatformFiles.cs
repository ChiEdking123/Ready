namespace Ready.Services;

/// <summary>
/// 备份的导出与导入需要各平台自己的能力：Android 走系统分享面板与文件选择器，
/// Windows 走保存/打开文件对话框。共享 UI 只决定“导出什么内容、导入进来怎么合并”，
/// 至于文件落到哪里，由每个宿主自己实现。
/// </summary>
public interface IPlatformFiles
{
    /// <summary>
    /// 把一段 JSON 交给用户保存。返回实际落盘或交给系统的路径；用户取消时返回 null。
    /// </summary>
    Task<string?> ExportAsync(string suggestedFileName, string json, CancellationToken cancellationToken = default);

    /// <summary>
    /// 让用户挑一个 JSON 备份并读出全文。用户取消时返回 null。
    /// </summary>
    Task<string?> ImportAsync(CancellationToken cancellationToken = default);
}
