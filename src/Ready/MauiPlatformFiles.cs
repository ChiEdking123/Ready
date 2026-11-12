using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace Ready;

/// <summary>
/// MAUI 头的备份实现：先把 JSON 写到缓存目录，再交给系统分享面板，由用户决定存到
/// 文件、网盘还是别处；导入则用系统文件选择器。Android 与 Windows 共用这一份。
/// </summary>
internal sealed class MauiPlatformFiles : IPlatformFiles
{
    public async Task<string?> ExportAsync(string suggestedFileName, string json, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, suggestedFileName);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        await Share.Default.RequestAsync(new ShareFileRequest(
            "导出 Ready! 构思备份",
            new ShareFile(path, "application/json")));
        return path;
    }

    public async Task<string?> ImportAsync(CancellationToken cancellationToken = default)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "选择 Ready! JSON 备份" });
        if (result is null) return null;

        using var stream = await result.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
