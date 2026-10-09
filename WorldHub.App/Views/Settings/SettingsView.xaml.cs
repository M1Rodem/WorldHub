using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WorldHub.App.Services.Network;
using WorldHub.App.Services.Settings;
using WorldHub.App.Services.Update;
using WorldHub.Infrastructure.Google;

namespace WorldHub.App.Views.Settings;

public partial class SettingsView : UserControl
{
    private DataMigrationService? _dataMigrationService;
    private UpdaterProcessService? _updaterProcessService;

    public SettingsView()
    {
        InitializeComponent();
    }

    public void Initialize(
        AppVersionService appVersionService,
        AppSettingsService appSettingsService,
        GoogleDriveClient googleDriveClient,
        GoogleDriveStatusCache googleDriveStatusCache)
    {
        ArgumentNullException.ThrowIfNull(appVersionService);
        ArgumentNullException.ThrowIfNull(appSettingsService);
        ArgumentNullException.ThrowIfNull(googleDriveClient);
        ArgumentNullException.ThrowIfNull(googleDriveStatusCache);

        _dataMigrationService = new DataMigrationService(appSettingsService);
        _updaterProcessService = new UpdaterProcessService();

        ProfileSection.Initialize(appSettingsService, googleDriveClient, googleDriveStatusCache);
        StorageSection.Initialize(appSettingsService, _dataMigrationService);
        UpdateSection.Initialize(appVersionService, _updaterProcessService);
    }

    private const string GitHubUrl =
        "https://github.com/M1Rodem/WorldHub";

    private void GitHubLinkButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = GitHubUrl,
                    UseShellExecute = true
                });
        }
        catch
        {
            // Если браузер не удалось открыть — молча игнорируем.
        }
    }
}