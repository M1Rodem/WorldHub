using System.IO;

namespace WorldHub.Network.Transfer;

/// <summary>
/// Временная директория для приёма/отправки снапшотов.
/// Автоматически удаляется при Dispose.
/// </summary>
public sealed class TransferStagingArea : IDisposable
{
    public string RootPath { get; }

    public TransferStagingArea(string rootPath, string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        RootPath = Path.Combine(rootPath, $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
    }

    public string CreateSubdirectory(string name)
    {
        var path = Path.Combine(RootPath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            try
            {
                Directory.Delete(RootPath, recursive: true);
            }
            catch (IOException)
            {
                // best-effort cleanup
            }
        }
    }
}