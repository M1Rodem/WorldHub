using WorldHub.App.Services.Settings;
using WorldHub.App.Services.Update;
using WorldHub.Core.Entities;
using WorldHub.Network.Services;

namespace WorldHub.App.Services.Network;

/// <summary>
/// Собирает WorldHubParticipant о текущем пользователе:
/// IP Radmin, DeviceId, ник, имя ПК, версия.
/// Используется при создании WorldHub-server — владелец
/// становится первым участником.
/// </summary>
public sealed class LocalParticipantProvider
{
    private readonly DeviceIdentityService _deviceIdentityService;
    private readonly AppSettingsService _settingsService;
    private readonly AppVersionService _versionService;
    private readonly RadminVpnDetector _radminVpnDetector;

    public LocalParticipantProvider(
        DeviceIdentityService deviceIdentityService,
        AppSettingsService settingsService,
        AppVersionService versionService,
        RadminVpnDetector radminVpnDetector)
    {
        ArgumentNullException.ThrowIfNull(deviceIdentityService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(versionService);
        ArgumentNullException.ThrowIfNull(radminVpnDetector);

        _deviceIdentityService = deviceIdentityService;
        _settingsService = settingsService;
        _versionService = versionService;
        _radminVpnDetector = radminVpnDetector;
    }

    /// <summary>
    /// Возвращает данные текущего пользователя.
    /// null, если Radmin VPN не подключён.
    /// </summary>
    public WorldHubParticipant? TryCreate()
    {
        var adapter = _radminVpnDetector.FindAdapter();

        if (adapter is null)
        {
            return null;
        }

        var deviceId = _deviceIdentityService.GetDeviceId();

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return new WorldHubParticipant
        {
            IpAddress = adapter.Address.ToString(),
            DeviceId = deviceId,
            UserName = _settingsService.GetUserName(),
            PcName = Environment.MachineName,
            WorldHubVersion = _versionService.GetVersion(),
            WorldHubStatus = "Online",
            IsPingAvailable = true,
            IsWorldHubResponding = true,
            LastCheckAtUtc = DateTimeOffset.UtcNow,
            LastSeenAtUtc = DateTimeOffset.UtcNow,
            FolderAccessStatus = Core.Enums.FolderAccessStatus.Unknown
        };
    }
}