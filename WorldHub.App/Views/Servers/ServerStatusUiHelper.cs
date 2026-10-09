using System.Windows.Media;
using WorldHub.Core.Enums;

namespace WorldHub.App.Views.Servers;

public static class ServerStatusUiHelper
{
    public static readonly Brush RunningBrush =
        new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));

    public static readonly Brush BusyBrush =
        new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6));

    public static readonly Brush ErrorBrush =
        new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));

    public static readonly Brush StoppedBrush =
        new SolidColorBrush(Color.FromRgb(0x8E, 0x9A, 0xB0));

    public static string GetStatusText(ServerStatus status) =>
        status switch
        {
            ServerStatus.Stopped => "Сервер остановлен",
            ServerStatus.Starting => "Сервер запускается",
            ServerStatus.Running => "Сервер запущен",
            ServerStatus.Stopping => "Сервер останавливается",
            ServerStatus.Error => "Ошибка сервера",
            _ => "Неизвестно"
        };

    public static Brush GetStatusBrush(ServerStatus status) =>
        status switch
        {
            ServerStatus.Running => RunningBrush,
            ServerStatus.Starting or ServerStatus.Stopping => BusyBrush,
            ServerStatus.Error => ErrorBrush,
            _ => StoppedBrush
        };

    public static string GetActionText(ServerStatus status) =>
        status == ServerStatus.Running
            ? "Остановить сервер"
            : "Запустить сервер";

    public static bool CanExecuteAction(ServerStatus status) =>
        status is ServerStatus.Stopped or ServerStatus.Running or ServerStatus.Error;
}
