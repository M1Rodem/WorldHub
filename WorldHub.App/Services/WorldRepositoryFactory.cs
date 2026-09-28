using System.IO;
using WorldHub.Infrastructure.Storage;

namespace WorldHub.App.Services;

public static class WorldRepositoryFactory
{
    public static JsonWorldRepository Create()
    {
        var dataPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "WorldHub",
            "data");

        var worldsPath = Path.Combine(
            dataPath,
            "worlds");

        return new JsonWorldRepository(worldsPath);
    }
}