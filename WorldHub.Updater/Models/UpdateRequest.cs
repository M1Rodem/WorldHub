namespace WorldHub.Updater.Models;

public sealed record UpdateRequest(
    string ApplicationPath,
    string CurrentVersion);