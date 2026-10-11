using MyProject.Shared.Abstractions;

namespace MyProject.Tests.Fakes;

class InMemoryFileStorage : IFileStorage
{
    public Dictionary<string, byte[]> Files = new();

    public async Task<string> UploadAsync(Stream file, string name, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        Files[name] = ms.ToArray();
        return name;
    }

    public Task<Stream?> DownloadAsync(string name, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(Files.TryGetValue(name, out var b) ? new MemoryStream(b) : null);

    public Task DeleteAsync(string name, CancellationToken ct = default)
    {
        Files.Remove(name);
        return Task.CompletedTask;
    }
}
