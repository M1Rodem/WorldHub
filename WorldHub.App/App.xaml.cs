using System.Windows;
using WorldHub.App.Services;

namespace WorldHub.App;

public partial class App : Application
{
    protected override void OnStartup(
        StartupEventArgs e)
    {
        DebugConsole.Initialize();

        DebugConsole.Log("WorldHub starting...");

        base.OnStartup(e);

        DebugConsole.Log("WorldHub UI initialized.");
    }

    protected override void OnExit(
        ExitEventArgs e)
    {
        DebugConsole.Log("WorldHub shutting down...");

        DebugConsole.Close();

        base.OnExit(e);
    }
}