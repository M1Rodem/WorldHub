using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using WorldHub.App.Services;

namespace WorldHub.App.Views;

public partial class ConnectionView : UserControl
{
    private WorldHubNetworkService? _networkService;

    private string? _ownAddress;

    public ConnectionView()
    {
        InitializeComponent();
    }

    public void Initialize(
        WorldHubNetworkService networkService)
    {
        ArgumentNullException.ThrowIfNull(networkService);

        _networkService = networkService;

        PortTextBox.Text = string.Empty;

        UpdateOwnAddress(networkService.Port);
    }

    private void UpdateOwnAddress(int port)
    {
        var ipAddress = FindRadminVpnAddress();

        if (ipAddress is null)
        {
            _ownAddress = null;

            OwnAddressTextBlock.Text =
                $"Radmin VPN не найден :{port}";

            OwnStatusTextBlock.Text =
                $"WorldHub запущен и ожидает подключения на порту {port}. " +
                "IP-адрес Radmin VPN не обнаружен.";

            CopyAddressButton.IsEnabled = false;

            DebugConsole.Log(
                "Radmin VPN IPv4 address was not found.");

            return;
        }

        _ownAddress =
            $"{ipAddress}:{port}";

        OwnAddressTextBlock.Text =
            _ownAddress;

        OwnStatusTextBlock.Text =
            $"Готов принимать подключения на порту {port}.";

        CopyAddressButton.IsEnabled = true;

        DebugConsole.Log(
            $"Radmin VPN address detected: {_ownAddress}");
    }

    private static string? FindRadminVpnAddress()
    {
        try
        {
            foreach (var networkInterface in
                     NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus !=
                    OperationalStatus.Up)
                {
                    continue;
                }

                if (networkInterface.NetworkInterfaceType ==
                    NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var properties =
                    networkInterface.GetIPProperties();

                foreach (var address in
                         properties.UnicastAddresses)
                {
                    if (address.Address.AddressFamily !=
                        AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    var ip =
                        address.Address;

                    if (ip.GetAddressBytes()[0] == 26)
                    {
                        return ip.ToString();
                    }
                }
            }
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to detect Radmin VPN address: {exception}");
        }

        return null;
    }

    private void CopyAddressButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_ownAddress))
        {
            return;
        }

        try
        {
            Clipboard.SetText(_ownAddress);

            OwnStatusTextBlock.Text =
                "Адрес скопирован в буфер обмена.";

            DebugConsole.Log(
                $"WorldHub address copied: {_ownAddress}");
        }
        catch (Exception exception)
        {
            OwnStatusTextBlock.Text =
                "Не удалось скопировать адрес.";

            DebugConsole.Error(
                $"Failed to copy WorldHub address: {exception}");
        }
    }

    private async void ConnectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_networkService is null)
        {
            SetConnectionStatus(
                "Сетевой сервис не инициализирован.");

            return;
        }

        var host =
            HostTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(host))
        {
            SetConnectionStatus(
                "Введите IP-адрес друга.");

            return;
        }

        if (!int.TryParse(
                PortTextBox.Text.Trim(),
                out var port) ||
            port is < 1 or > 65535)
        {
            SetConnectionStatus(
                "Введите корректный порт WorldHub друга.");

            return;
        }

        ConnectButton.IsEnabled = false;

        var target =
            $"{host}:{port}";

        SetConnectionStatus(
            $"Подключение к {target}...");

        DebugConsole.Log(
            $"Connection UI requested: {target}");

        try
        {
            var connected =
                await _networkService.ConnectAsync(
                    host,
                    port);

            if (connected)
            {
                SetConnectionStatus(
                    $"Проверка связи успешна: {target}");

                DebugConsole.Log(
                    $"Connection UI handshake succeeded: {target}");
            }
            else
            {
                SetConnectionStatus(
                    $"Не удалось подключиться к {target}");

                DebugConsole.Log(
                    $"Connection UI handshake failed: {target}");
            }
        }
        catch (Exception exception)
        {
            SetConnectionStatus(
                $"Ошибка подключения: {exception.Message}");

            DebugConsole.Error(
                $"Connection UI error: {exception}");
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    public bool TryGetFriendEndpoint(
        out string host,
        out int port)
    {
        host = HostTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(host))
        {
            port = 0;
            return false;
        }

        if (!int.TryParse(
                PortTextBox.Text.Trim(),
                out port) ||
            port is < 1 or > 65535)
        {
            port = 0;
            return false;
        }

        return true;
    }

    private void SetConnectionStatus(
        string message)
    {
        StatusTextBlock.Text = message;
    }
}