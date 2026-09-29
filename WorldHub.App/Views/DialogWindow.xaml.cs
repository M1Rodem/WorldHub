using System.Windows;

namespace WorldHub.App.Views;

public partial class DialogWindow : Window
{
    public DialogWindow(
        string title,
        string message,
        Window? owner = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;

        if (owner is not null)
        {
            Owner = owner;
        }
    }

    private void OkButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}