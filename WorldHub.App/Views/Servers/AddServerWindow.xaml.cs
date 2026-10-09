using Microsoft.Win32;
using System.IO;
using System.Windows;
using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;
using WorldHub.Sync.Services;

namespace WorldHub.App.Views.Servers;

public partial class AddServerWindow
{
    private readonly ServerService _serverService;
    private readonly IServerDetector _serverDetector;

    private ServerDetectionResult? _detectionResult;

    public AddServerWindow(
        ServerService serverService,
        IServerDetector serverDetector)
    {
        ArgumentNullException.ThrowIfNull(serverService);
        ArgumentNullException.ThrowIfNull(serverDetector);

        _serverService = serverService;
        _serverDetector = serverDetector;

        InitializeComponent();
    }

    private void SelectLaunchFileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите файл запуска Minecraft Dedicated Server",
            Filter =
                "Файлы запуска|*.bat;*.cmd;*.jar|" +
                "Batch-файлы|*.bat;*.cmd|" +
                "Java Archive|*.jar|" +
                "Все файлы|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var detection =
                _serverDetector.Detect(
                    dialog.FileName);

            _detectionResult =
                detection;

            LaunchFileTextBox.Text =
                dialog.FileName;

            if (string.IsNullOrWhiteSpace(
                    NameTextBox.Text))
            {
                NameTextBox.Text =
                    new DirectoryInfo(
                        detection.ServerDirectory).Name;
            }

            MinecraftVersionTextBlock.Text =
                detection.MinecraftVersion;

            LoaderTextBlock.Text =
                detection.Loader;

            LoaderVersionTextBlock.Text =
                detection.LoaderVersion ?? "—";

            LaunchTypeTextBlock.Text =
                detection.LaunchConfiguration.Type
                    .ToString();

            DetectionPanel.Visibility =
                Visibility.Visible;
        }
        catch (Exception exception)
        {
            _detectionResult = null;

            DetectionPanel.Visibility =
                Visibility.Collapsed;

            MessageBox.Show(
                this,
                exception.Message,
                "Не удалось определить сервер",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void AddButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            var name =
                NameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowValidationError(
                    "Введите название сервера.");

                return;
            }

            if (_detectionResult is null)
            {
                ShowValidationError(
                    "Сначала выберите файл запуска сервера.");

                return;
            }

            await _serverService.CreateAsync(
                name,
                _detectionResult);

            DialogResult = true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Не удалось добавить сервер",
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

    private void ShowValidationError(
        string message)
    {
        MessageBox.Show(
            this,
            message,
            "Проверьте данные",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}