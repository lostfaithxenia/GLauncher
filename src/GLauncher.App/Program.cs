using System.Security.Cryptography;
using System.Text;
using Microsoft.UI.Xaml;

namespace GLauncher.App;

internal static class Program
{
    internal static string[] Arguments = [];
    internal static string DataDirectory = "";
    internal static void Trace(string message) => File.AppendAllText(Path.Combine(DataDirectory, "startup.log"), DateTimeOffset.Now + " " + message + Environment.NewLine);
    internal static string? Option(string name)
    {
        int i = Array.IndexOf(Arguments, name);
        return i >= 0 && i + 1 < Arguments.Length ? Arguments[i + 1] : null;
    }
    [STAThread]
    public static void Main(string[] args)
    {
        Arguments = args;
        if (Option("--smoke-test") is not null && Option("--data-directory") is null) { Environment.ExitCode = 2; return; }
        DataDirectory = Path.GetFullPath(Option("--data-directory") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GLauncher"));
        Directory.CreateDirectory(DataDirectory);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DataDirectory.ToUpperInvariant())))[..24];
        using var singleton = new Mutex(true, "Local\\GLauncher-" + key, out bool acquired);
        if (!acquired) return;
        try
        {
            Trace("Main entered");
            if (Option("--smoke-test") is not null)
                AppDomain.CurrentDomain.FirstChanceException += (_, e) => Trace("First chance: " + e.Exception);
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Trace("COM wrappers ready");
            Application.Start(parameters =>
            {
                SynchronizationContext.SetSynchronizationContext(new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));
                _ = new LauncherApp();
            });
        }
        catch (Exception error) { File.AppendAllText(Path.Combine(DataDirectory, "error.log"), DateTimeOffset.Now + " " + error + Environment.NewLine); Environment.ExitCode = 1; }
        finally { singleton.ReleaseMutex(); }
    }
}

public sealed partial class LauncherApp : Application
{
    private MainWindow? main;
    public LauncherApp()
    {
        UnhandledException += (_, e) => File.AppendAllText(Path.Combine(Program.DataDirectory, "error.log"), DateTimeOffset.Now + " " + e.Exception + Environment.NewLine);
        Program.Trace("Application constructed");
        InitializeComponent();
        Program.Trace("Application XAML loaded");
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Program.Trace("OnLaunched");
        try { main = new MainWindow(); Program.Trace("Window constructed"); main.Activate(); Program.Trace("Window activated"); }
        catch (Exception error) { Program.Trace(error.ToString()); Environment.ExitCode = 1; Exit(); }
    }
}
