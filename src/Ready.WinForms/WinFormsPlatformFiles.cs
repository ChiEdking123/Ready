using System.IO;
using System.Windows.Forms;

namespace Ready;

/// <summary>
/// Windows 头的备份实现：导出走「另存为」，导入走「打开」。
/// </summary>
internal sealed class WinFormsPlatformFiles : IPlatformFiles
{
    public Task<string?> ExportAsync(string suggestedFileName, string json, CancellationToken cancellationToken = default)
    {
        using var dialog = new SaveFileDialog
        {
            Title = "导出 Ready! 构思备份",
            FileName = suggestedFileName,
            DefaultExt = "json",
            Filter = "JSON 备份 (*.json)|*.json|所有文件 (*.*)|*.*",
            AddExtension = true,
            OverwritePrompt = true,
        };

        if (dialog.ShowDialog(Owner) != DialogResult.OK) return Task.FromResult<string?>(null);
        return WriteAsync(dialog.FileName, json, cancellationToken);
    }

    private static async Task<string?> WriteAsync(string path, string json, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(path, json, cancellationToken);
        return path;
    }

    public async Task<string?> ImportAsync(CancellationToken cancellationToken = default)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 Ready! JSON 备份",
            Filter = "JSON 备份 (*.json)|*.json|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(Owner) != DialogResult.OK) return null;
        return await File.ReadAllTextAsync(dialog.FileName, cancellationToken);
    }

    private static IWin32Window? Owner =>
        Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
}
