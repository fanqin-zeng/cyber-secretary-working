using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using CyberSecretary.Core;

namespace CyberSecretary;

public partial class App : Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CyberSecretary");
        int index = Array.IndexOf(e.Args, "--data-dir");
        if (index >= 0 && index + 1 < e.Args.Length) directory = Path.GetFullPath(e.Args[index + 1]);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(directory.ToUpperInvariant())))[..20];
        _mutex = new Mutex(true, "Local\\CyberSecretary-" + key, out _ownsMutex);
        if (!_ownsMutex)
        {
            MessageBox.Show("CyberSecretray::Working已在运行，请切换至已打开的窗口。", "CyberSecretray::Working", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(); return;
        }
        var store = new TaskStore(directory);
        try
        {
            var tasks = store.Load();
            if (e.Args.Contains("--demo") && !File.Exists(store.FilePath))
            {
                tasks = DemoTasks.Create(); store.Save(tasks);
            }
            var window = new MainWindow(store, tasks);
            MainWindow = window;
            window.Show();
            if (store.RecoveredFromBackup) window.ShowNotice("已从上一次备份恢复，原文件已保留。", true);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show("无法读取任务数据，原文件已保留。\n\n" + ex.Message + "\n\n数据位置：" + directory, "CyberSecretray::Working", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
