
using System.IO;

namespace WorldHub.App.Services.Network;

public sealed class DeviceIdentityService
{
    private const string FileName = "device-id.txt";

    private readonly string _filePath;

    public DeviceIdentityService(string dataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataPath);

        Directory.CreateDirectory(dataPath);
        _filePath = Path.Combine(dataPath, FileName);
    }

    public string GetDeviceId()
    {
        if (File.Exists(_filePath))
        {
            var savedId = File.ReadAllText(_filePath).Trim();

            if (Guid.TryParse(savedId, out var id))
            {
                return id.ToString("D");
            }
        }

        var newId = Guid.NewGuid().ToString("D");
        File.WriteAllText(_filePath, newId);

        return newId;
    }
}
