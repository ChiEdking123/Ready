using System.IO;

namespace Ready;

/// <summary>
/// 本机的数据落点。刻意不用 MAUI 的 FileSystem.AppDataDirectory：那条路径是
/// <c>%LOCALAPPDATA%\&lt;发布者&gt;\&lt;应用名&gt;\Data</c>，带着发布者前缀，
/// 而且只有 MAUI 头认得。Windows 头用自己的固定目录，并在首次启动时把旧数据复制过来。
/// </summary>
internal static class ReadyPaths
{
    private static string? _dataFile;

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Ready");

    public static string DataFile => _dataFile ??= ResolveDataFile();

    private static string ResolveDataFile()
    {
        var directory = DataDirectory;
        try { Directory.CreateDirectory(directory); } catch (IOException) { }

        var target = Path.Combine(directory, "ready-data.json");
        if (!File.Exists(target)) TryMigrateFromMauiHead(target);
        return target;
    }

    /// <summary>
    /// 旧 MAUI 头把数据放在 %LOCALAPPDATA%\&lt;发布者&gt;\&lt;应用名&gt;\Data\ready-data.json。
    /// 第一次用 Windows 头启动时复制一份过来，用户不会看到「构思全没了」。
    /// 只复制不搬移：万一还要退回 MAUI 头，原文件仍在。
    /// </summary>
    private static void TryMigrateFromMauiHead(string target)
    {
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            foreach (var publisher in Directory.EnumerateDirectories(local))
            {
                string[] apps;
                try { apps = Directory.GetDirectories(publisher); }
                catch (Exception) { continue; }

                foreach (var app in apps)
                {
                    var candidate = Path.Combine(app, "Data", "ready-data.json");
                    if (!File.Exists(candidate)) continue;
                    File.Copy(candidate, target);
                    return;
                }
            }
        }
        catch (Exception)
        {
            // 迁移是加分项：失败就从一个空工作台开始，不能因此启动不了。
        }
    }

    public static void AppendCrashLog(Exception exception)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(DataDirectory, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // 记日志失败就算了，别在异常处理里再抛。
        }
    }
}
