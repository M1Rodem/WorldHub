using System.Windows;

namespace WorldHub.App.Views.Dialogs;

public partial class SyncProgressWindow : Window
{
    public SyncProgressWindow(string title, string subtitle, string iconPath = "/Assets/Icons/cloud.svg", Window? owner = null)
    {
        InitializeComponent();

        TitleText.Text = title;
        SubtitleText.Text = subtitle;

        try
        {
            OperationIcon.Source = new Uri(iconPath, UriKind.Relative);
        }
        catch
        {
        }

        if (owner is not null)
        {
            Owner = owner;
        }
    }

    public void ReportProgress(double percentage)
    {
        Dispatcher.Invoke(() =>
        {
            var clamped = Math.Clamp(percentage, 0.0, 100.0);
            SyncProgressBar.Value = clamped;
            PercentageText.Text = $"{clamped:0}%";
        });
    }

    public void ReportStatus(string message)
    {
        Dispatcher.Invoke(() =>
        {
            StatusMessageText.Text = message;
        });
    }

    public void Complete(string message, bool isSuccess = true)
    {
        Dispatcher.Invoke(() =>
        {
            SyncProgressBar.Value = 100;
            PercentageText.Text = "100%";
            StatusMessageText.Text = message;
            CloseButton.Visibility = Visibility.Visible;
            if (!isSuccess)
            {
                PercentageText.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
