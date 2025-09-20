using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var ni = new NotifyIcon();
        ni.Icon = SystemIcons.Application;
        ni.Visible = true;
        ni.Text = "OBS Music Overlay";

        var menu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem("Открыть оверлей");
        openItem.Click += (_, __) => TryOpen("http://localhost:3000/?w=560");
        var restartItem = new ToolStripMenuItem("Перезапустить сервер");
        restartItem.Click += (_, __) => RestartServer();
        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (_, __) => { StopServer(); ni.Visible = false; Application.Exit(); };
        menu.Items.Add(openItem);
        menu.Items.Add(restartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        ni.ContextMenuStrip = menu;

        StartServer();
        Application.Run();
    }

    static Process? _server;

    static void StartServer()
    {
        StopServer();
        var exeDir = AppContext.BaseDirectory;
        string serverExe = Path.Combine(exeDir, "obs-music-overlay.exe");
        if (!File.Exists(serverExe))
        {
            // fallback: запуск из node (на случай отладки)
            _server = Process.Start(new ProcessStartInfo
            {
                FileName = "node",
                Arguments = "src/server/index.js",
                WorkingDirectory = exeDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return;
        }
        _server = Process.Start(new ProcessStartInfo
        {
            FileName = serverExe,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    static void StopServer()
    {
        try
        {
            if (_server != null && !_server.HasExited)
            {
                _server.Kill(true);
                _server.Dispose();
            }
        }
        catch { }
        finally { _server = null; }
    }

    static void RestartServer()
    {
        StopServer();
        StartServer();
    }

    static void TryOpen(string url)
    {
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { }
    }
}


