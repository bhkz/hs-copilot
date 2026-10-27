using System.Windows;

namespace HsCopilot.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "HS Copilot — erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        var options = StartupOptions.Parse(e.Args);
        // --bench <dossier> : fenêtre d'examen du banc d'essai seule (aucune source en direct n'est ouverte).
        Window window = options.BenchDir is not null ? new BenchWindow(options.BenchDir) : new MainWindow(options);
        MainWindow = window;
        window.Show();
    }
}

/// <summary>Arguments : --config &lt;copilot.json&gt;, --replay &lt;fichier&gt;, --turn &lt;N&gt;, --source auto|hdt|log|none, --bench &lt;dossier&gt;.</summary>
public sealed class StartupOptions
{
    public string? ConfigPath { get; private init; }
    public string? ReplayPath { get; private init; }
    public int? Turn { get; private init; }
    public string? Source { get; private init; }
    public string? BenchDir { get; private init; }

    public static StartupOptions Parse(string[] args)
    {
        string? Get(string key)
        {
            var i = Array.FindIndex(args, a => a.Equals(key, StringComparison.OrdinalIgnoreCase));
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        return new StartupOptions
        {
            ConfigPath = Get("--config"),
            ReplayPath = Get("--replay"),
            Turn = int.TryParse(Get("--turn"), out var t) ? t : null,
            Source = Get("--source"),
            BenchDir = Get("--bench"),
        };
    }
}
