using System.Text.Json;
using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;

namespace WorldHub.Infrastructure.Storage;

public sealed class JsonTransferRepository : ITransferRepository
{
    private readonly string _transfersRootPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonTransferRepository(string transfersRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transfersRootPath);

        _transfersRootPath = transfersRootPath;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        Directory.CreateDirectory(_transfersRootPath);
    }

    public async Task<Transfer?> GetByIdAsync(
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(transferId);

        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return await JsonSerializer.DeserializeAsync<Transfer>(
            stream,
            _jsonOptions,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<Transfer>> GetByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var files = Directory.EnumerateFiles(
            _transfersRootPath,
            "*.json",
            SearchOption.TopDirectoryOnly);

        var transfers = new List<Transfer>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var transfer = await JsonSerializer.DeserializeAsync<Transfer>(
                stream,
                _jsonOptions,
                cancellationToken);

            if (transfer is not null && transfer.WorldId == worldId)
            {
                transfers.Add(transfer);
            }
        }

        return transfers
            .OrderByDescending(x => x.StartedAt)
            .ToArray();
    }

    public async Task AddAsync(
        Transfer transfer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        var filePath = GetFilePath(transfer.Id);

        if (File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"Transfer '{transfer.Id}' already exists.");
        }

        await WriteAsync(
            filePath,
            transfer,
            cancellationToken);
    }

    public async Task UpdateAsync(
        Transfer transfer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        var filePath = GetFilePath(transfer.Id);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Transfer '{transfer.Id}' was not found.",
                filePath);
        }

        await WriteAsync(
            filePath,
            transfer,
            cancellationToken);
    }

    private async Task WriteAsync(
        string filePath,
        Transfer transfer,
        CancellationToken cancellationToken)
    {
        var temporaryFilePath = $"{filePath}.tmp";

        await using (
            var stream = new FileStream(
                temporaryFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                transfer,
                _jsonOptions,
                cancellationToken);
        }

        File.Move(
            temporaryFilePath,
            filePath,
            overwrite: true);
    }

    private string GetFilePath(Guid transferId)
    {
        return Path.Combine(
            _transfersRootPath,
            $"{transferId}.json");
    }
}