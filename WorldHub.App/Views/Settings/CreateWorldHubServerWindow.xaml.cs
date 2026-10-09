using System.Windows;
using WorldHub.App.Services.Network;
using WorldHub.Core.Entities;
using WorldHub.Sync.Services;

namespace WorldHub.App.Views.Settings;

public partial class CreateWorldHubServerWindow
{
    private readonly WorldHubServerService _worldHubServerService;
    private readonly LocalParticipantProvider _localParticipantProvider;

    public WorldHubServer? CreatedServer { get; private set; }

    public CreateWorldHubServerWindow(
        WorldHubServerService worldHubServerService,
        LocalParticipantProvider localParticipantProvider)
    {
        ArgumentNullException.ThrowIfNull(worldHubServerService);
        ArgumentNullException.ThrowIfNull(localParticipantProvider);

        _worldHubServerService = worldHubServerService;
        _localParticipantProvider = localParticipantProvider;

        InitializeComponent();

        Loaded += (_, _) =>
            NameTextBox.Focus();
    }

    private async void CreateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var name = NameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(
                    this,
                    "Введите название WorldHub-server.",
                    "Проверьте данные",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var owner = _localParticipantProvider.TryCreate();

            if (owner is null)
            {
                MessageBox.Show(
                    this,
                    "Не удалось определить локального участника.\n\n" +
                    "Убедитесь, что Radmin VPN подключён.",
                    "WorldHub",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            CreatedServer =
                await _worldHubServerService.CreateAsync(name, owner);

            DialogResult = true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Не удалось создать WorldHub-server",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
    }
}