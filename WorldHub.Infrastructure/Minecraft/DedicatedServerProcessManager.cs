using System.Diagnostics;
using WorldHub.Core.Entities;
using WorldHub.Core.Enums;
using WorldHub.Infrastructure.Windows;

namespace WorldHub.Infrastructure.Minecraft;

public sealed class DedicatedServerProcessManager
{
    private readonly ServerProcessLauncher _launcher;
    private readonly WindowsConsoleService _consoleService;

    private readonly Dictionary<Guid, Process> _processes = new();
    private readonly Dictionary<Guid, ServerStatus> _statuses = new();

    private readonly object _sync = new();

    public DedicatedServerProcessManager(
        ServerProcessLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);

        _launcher = launcher;
        _consoleService = new WindowsConsoleService();
    }

    public ServerStatus GetStatus(Guid serverId)
    {
        lock (_sync)
        {
            if (!_processes.TryGetValue(serverId, out var process))
            {
                return _statuses.TryGetValue(serverId, out var status)
                    ? status
                    : ServerStatus.Stopped;
            }

            if (process.HasExited)
            {
                _processes.Remove(serverId);
                _statuses[serverId] = ServerStatus.Stopped;
                process.Dispose();

                return ServerStatus.Stopped;
            }

            return _statuses.TryGetValue(serverId, out var currentStatus)
                ? currentStatus
                : ServerStatus.Stopped;
        }
    }

    public bool IsRunning(Server server)
    {
        ArgumentNullException.ThrowIfNull(server);

        return GetStatus(server.Id) == ServerStatus.Running;
    }

    public Process? GetProcess(Guid serverId)
    {
        lock (_sync)
        {
            return _processes.TryGetValue(serverId, out var process)
                && !process.HasExited
                ? process
                : null;
        }
    }

    public async Task StartAsync(
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);

        lock (_sync)
        {
            var currentStatus = GetStatusUnsafe(server.Id);

            if (currentStatus is
                ServerStatus.Starting or
                ServerStatus.Running or
                ServerStatus.Stopping)
            {
                throw new InvalidOperationException(
                    "Minecraft server is already running or starting.");
            }

            _statuses[server.Id] = ServerStatus.Starting;
        }

        var logPath = Path.Combine(
            server.LocalPath,
            "logs",
            "latest.log");

        var initialLogLength = File.Exists(logPath)
            ? new FileInfo(logPath).Length
            : 0;

        Process? process = null;

        try
        {
            process = _launcher.Launch(server);

            lock (_sync)
            {
                _processes[server.Id] = process;
            }

            await WaitForReadyAsync(
                server.Id,
                logPath,
                initialLogLength,
                cancellationToken);

            lock (_sync)
            {
                _statuses[server.Id] = ServerStatus.Running;
            }
        }
        catch
        {
            lock (_sync)
            {
                _statuses[server.Id] = ServerStatus.Error;
            }

            if (process is not null)
            {
                TryKillProcess(process);

                lock (_sync)
                {
                    _processes.Remove(server.Id);
                }

                process.Dispose();
            }

            throw;
        }
    }

    public async Task StopAsync(
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);

        Process? process;

        lock (_sync)
        {
            if (!_processes.TryGetValue(server.Id, out process)
                || process.HasExited)
            {
                _statuses[server.Id] = ServerStatus.Stopped;

                if (process is not null)
                {
                    _processes.Remove(server.Id);
                    process.Dispose();
                }

                return;
            }

            _statuses[server.Id] = ServerStatus.Stopping;
        }

        try
        {
            _consoleService.SendCommand(
                process.Id,
                "stop");

            await process.WaitForExitAsync(cancellationToken);

            lock (_sync)
            {
                _processes.Remove(server.Id);
                _statuses[server.Id] = ServerStatus.Stopped;
            }

            process.Dispose();
        }
        catch
        {
            lock (_sync)
            {
                _statuses[server.Id] = ServerStatus.Error;
            }

            throw;
        }
    }

    private async Task WaitForReadyAsync(
        Guid serverId,
        string logPath,
        long initialPosition,
        CancellationToken cancellationToken)
    {
        const string readyMarker = "available and ready to play";

        var position = initialPosition;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Process? process;

            lock (_sync)
            {
                _processes.TryGetValue(serverId, out process);
            }

            if (process is null || process.HasExited)
            {
                throw new InvalidOperationException(
                    "Minecraft server exited before becoming ready.");
            }

            if (!File.Exists(logPath))
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(250),
                    cancellationToken);

                continue;
            }

            try
            {
                await using var stream = new FileStream(
                    logPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                if (stream.Length < position)
                {
                    position = 0;
                }

                stream.Position = position;

                var buffer = new byte[8192];

                var bytesRead = await stream.ReadAsync(
                    buffer,
                    cancellationToken);

                if (bytesRead > 0)
                {
                    position = stream.Position;

                    var text = System.Text.Encoding.UTF8.GetString(
                        buffer,
                        0,
                        bytesRead);

                    if (text.Contains(
                        readyMarker,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
            }
            catch (IOException)
            {
                // Minecraft may currently be writing to the log.
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(250),
                cancellationToken);
        }
    }

    private ServerStatus GetStatusUnsafe(Guid serverId)
    {
        if (!_processes.TryGetValue(serverId, out var process))
        {
            return _statuses.TryGetValue(serverId, out var status)
                ? status
                : ServerStatus.Stopped;
        }

        if (process.HasExited)
        {
            _processes.Remove(serverId);
            _statuses[serverId] = ServerStatus.Stopped;
            process.Dispose();

            return ServerStatus.Stopped;
        }

        return _statuses.TryGetValue(serverId, out var currentStatus)
            ? currentStatus
            : ServerStatus.Stopped;
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort cleanup after failed startup.
        }
    }
}