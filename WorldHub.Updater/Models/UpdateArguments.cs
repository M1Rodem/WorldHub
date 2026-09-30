namespace WorldHub.Updater.Models;

public sealed record UpdateArguments(
    string ApplicationPath,
    string PackagePath);