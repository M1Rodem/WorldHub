using System.Windows.Controls;
using WorldHub.Network.Services;

namespace WorldHub.App.Views.Connection;

public partial class RadminVpnStatusCard : UserControl
{
    private RadminVpnDetector? _detector;

    public RadminVpnStatusCard()
    {
        InitializeComponent();
    }

    public void Initialize(RadminVpnDetector detector)
    {
        ArgumentNullException.ThrowIfNull(detector);
        _detector = detector;
    }

    public async Task RefreshStatusAsync()
    {
        if (_detector is null)
        {
            return;
        }

        RadminVpnStatusText.Text = "Проверка...";
        RadminVpnAddressText.Text = "IP: —";

        RadminVpnAdapterInfo? adapter;

        try
        {
            adapter = await Task.Run(() => _detector.FindAdapter());
        }
        catch (Exception exception)
        {
            RadminVpnStatusText.Text = $"Ошибка проверки Radmin: {exception.Message}";
            RadminVpnAddressText.Text = "IP: —";
            return;
        }

        if (adapter is null)
        {
            RadminVpnStatusText.Text = "Radmin VPN не обнаружен";
            RadminVpnAddressText.Text = "IP: —";
            return;
        }

        RadminVpnStatusText.Text = "Radmin VPN подключён";
        RadminVpnAddressText.Text = $"IP: {adapter.Address}";
    }
}
