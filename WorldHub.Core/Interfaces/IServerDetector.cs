using WorldHub.Core.Entities;

namespace WorldHub.Core.Interfaces;

public interface IServerDetector
{
    ServerDetectionResult Detect(string launchFilePath);
}