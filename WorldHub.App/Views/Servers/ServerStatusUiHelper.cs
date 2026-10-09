using System.Windows.Media;
using WorldHub.Core.Enums;

namespace WorldHub.App.Views.Servers;

public static class ServerStatusUiHelper
{
    public static readonly Brush RunningBrush =
        new SolidColorBrush(Color.FromRgb(0x42, 0xC7, 0x83));

    public static readonly Brush BusyBrush =
        new SolidColorBrush(Color.FromRgb(0x4C, 0x8D, 0xFF));

    public static readonly Brush ErrorBrush =
        new SolidColorBrush(Color.FromRgb(0xE0, 0x5C, 0x5C));

    public static readonly Brush StoppedBrush =
        new SolidColorBrush(Color.FromRgb(0x8C, 0x95, 0xA3));

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
