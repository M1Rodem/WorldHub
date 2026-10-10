using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace WorldHub.App.Views.Dialogs;

public partial class DialogWindow : Window
{
    private readonly DialogMode _mode;

    private DialogWindow(
        string title,
        string message,
        DialogMode mode,
        Window? owner = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        InitializeComponent();

        _mode = mode;

        TitleText.Text = title;
        MessageText.Text = message;

        if (owner is not null)
        {
            Owner = owner;
        }
    }

    public DialogWindow(
        string title,
        string message,
        Window? owner = null)
        : this(
            title,
            message,
            DialogMode.Information,
            owner)
    {
    }

    public static bool ShowConfirmation(
        Window? owner,
        string title,
        string message)
    {
        var dialog =
            new DialogWindow(
                title,
                message,
                DialogMode.Confirmation,
                owner);

        dialog.SecondaryButton.Visibility =
            Visibility.Visible;

        dialog.SecondaryButton.Content =
            "Нет";

        dialog.PrimaryButton.Content =
            "Да";

        return dialog.ShowDialog() == true;
    }

    public static bool ShowPathConfirmation(
        Window owner,
        string fromPath,
        string toPath)
    {
        var dialog =
            new DialogWindow(
                "Изменение папки данных",
                "Перенести данные WorldHub?",
                DialogMode.Confirmation,
                owner);

        dialog.PathPanel.Visibility =
            Visibility.Visible;

        dialog.FromPathText.Text =
            fromPath;

        dialog.ToPathText.Text =
            toPath;

        dialog.SecondaryButton.Visibility =
            Visibility.Visible;

        dialog.SecondaryButton.Content =
            "Нет";

        dialog.PrimaryButton.Content =
            "Перенести";

        return dialog.ShowDialog() == true;
    }

    public static bool ShowTransferConfirmation(
        Window owner,
        string worldName,
        int snapshotCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldName);

        var dialog = new DialogWindow(
            "Вам передали мир",
            $"Мир «{worldName}» хотят передать вам.\n\n" +
            $"Будет передано snapshot'ов: {snapshotCount}.",
            DialogMode.Confirmation,
            owner);

        dialog.SecondaryButton.Visibility =
            Visibility.Visible;

        dialog.SecondaryButton.Content =
            "Отклонить";

        dialog.PrimaryButton.Content =
            "Принять";

        return dialog.ShowDialog() == true;
    }

    public static void ShowInformation(
        Window? owner,
        string title,
        string message)
    {
        var dialog =
            new DialogWindow(
                title,
                message,
                DialogMode.Information,
                owner);

        dialog.ShowDialog();
    }

    public static void ShowError(
        Window? owner,
        string title,
        string message)
    {
        var dialog =
            new DialogWindow(
                title,
                message,
                DialogMode.Information,
                owner);

        dialog.IconText.Text =
            "!";

        dialog.IconText.Foreground =
            new SolidColorBrush(
                Color.FromRgb(255, 107, 129));

        dialog.IconBorder.Background =
            new SolidColorBrush(
                Color.FromRgb(58, 24, 32));

        dialog.IconBorder.BorderBrush =
            new SolidColorBrush(
                Color.FromRgb(143, 48, 67));

        dialog.ShowDialog();
    }

    public static void ShowWarning(
        Window? owner,
        string title,
        string message)
    {
        var dialog =
            new DialogWindow(
                title,
                message,
                DialogMode.Information,
                owner);

        dialog.IconText.Text =
            "!";

        dialog.IconText.Foreground =
            new SolidColorBrush(
                Color.FromRgb(255, 193, 92));

        dialog.IconBorder.Background =
            new SolidColorBrush(
                Color.FromRgb(58, 46, 24));

        dialog.IconBorder.BorderBrush =
            new SolidColorBrush(
                Color.FromRgb(128, 103, 52));

        dialog.ShowDialog();
    }

    public static bool ShowCompleted(
        Window owner,
        string newPath)
    {
        var dialog =
            new DialogWindow(
                "Перенос завершён",
                "Все данные WorldHub успешно перенесены.\n\n" +
                $"Новое расположение:\n{newPath}\n\n" +
                "Старая папка WorldHub удалена.",
                DialogMode.Completed,
                owner);

        dialog.IconText.Text =
            "✓";

        dialog.IconText.Foreground =
            new SolidColorBrush(
                Color.FromRgb(104, 211, 145));

        dialog.IconBorder.Background =
            new SolidColorBrush(
                Color.FromRgb(18, 55, 43));

        dialog.IconBorder.BorderBrush =
            new SolidColorBrush(
                Color.FromRgb(42, 111, 80));

        dialog.SecondaryButton.Visibility =
            Visibility.Visible;

        dialog.SecondaryButton.Content =
            "Закрыть";

        dialog.PrimaryButton.Content =
            "Перезапустить";

        return dialog.ShowDialog() == true;
    }

    private void PrimaryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        switch (_mode)
        {
            case DialogMode.Information:
                Close();
                break;

            case DialogMode.Confirmation:
                DialogResult = true;
                break;

            case DialogMode.Completed:
                RestartApplication();
                break;
        }
    }

    private void SecondaryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static void RestartApplication()
    {
        var executablePath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            Application.Current.Shutdown();
            return;
        }

        var currentPid = Environment.ProcessId;

        var escapedPath =
            executablePath.Replace("'", "''");

        var script =
            $"Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; " +
            $"Start-Process -FilePath '{escapedPath}'";

        var started = false;

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments =
                        $"-NoProfile -WindowStyle Hidden -Command \"{script}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

            started = true;
        }
        catch
        {
            // Не удалось запустить powershell.
        }

        if (!started)
        {
            var owner = Application.Current.MainWindow;

            if (owner is not null)
            {
                MessageBox.Show(
                    owner,
                    "WorldHub не удалось перезапустить автоматически.\n\n" +
                    "Запустите приложение вручную.",
                    "WorldHub",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        Application.Current.Shutdown();
    }

    public static bool ShowRestartRequired(
        Window owner,
        string newPath)
    {
        var dialog =
            new DialogWindow(
                "Перенос завершён",
                "Все данные WorldHub успешно перенесены.\n\n" +
                $"Новое расположение:\n{newPath}\n\n" +
                "Старая папка WorldHub удалена.\n\n" +
                "WorldHub нужно перезапустить, чтобы службы " +
                "подключились к новому расположению.",
                DialogMode.Completed,
                owner);

        dialog.IconText.Text =
            "✓";

        dialog.IconText.Foreground =
            new SolidColorBrush(
                Color.FromRgb(104, 211, 145));

        dialog.IconBorder.Background =
            new SolidColorBrush(
                Color.FromRgb(18, 55, 43));

        dialog.IconBorder.BorderBrush =
            new SolidColorBrush(
                Color.FromRgb(42, 111, 80));

        dialog.SecondaryButton.Visibility =
            Visibility.Collapsed;

        dialog.PrimaryButton.Content =
            "Перезапустить";

        return dialog.ShowDialog() == true;
    }

    private enum DialogMode
    {
        Information,
        Confirmation,
        Completed
    }
}