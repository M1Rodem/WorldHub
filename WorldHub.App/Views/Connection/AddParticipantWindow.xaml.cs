using System.Net;
using System.Windows;

namespace WorldHub.App.Views.Connection;

public partial class AddParticipantWindow
{
    public AddParticipantWindow()
    {
        InitializeComponent();

        Loaded += (_, _) => IpAddressTextBox.Focus();
    }

    public string? IpAddress { get; private set; }

    private void AddButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var value = IpAddressTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            ShowError("Введите IP-адрес.");
            return;
        }

        if (!IPAddress.TryParse(value, out var parsed) ||
            parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            ShowError("Некорректный IPv4-адрес.");
            return;
        }

        IpAddress = parsed.ToString();
        DialogResult = true;
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}