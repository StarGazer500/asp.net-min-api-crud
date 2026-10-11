using MyProject.Shared.Abstractions;

namespace MyProject.Adapters.Local;

// Development-only stand-in for S3: stores files under <content root>/uploads.
internal class LocalFileStorage(IWebHostEnvironment env) : IFileStorage
{
    private readonly string _root = Path.Combine(env.ContentRootPath, "uploads");

    public async Task<string> UploadAsync(Stream file, string name, CancellationToken ct = default)
    {
        var path = Resolve(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var target = File.Create(path);
        await file.CopyToAsync(target, ct);
        return name;
    }

    public Task<Stream?> DownloadAsync(string name, CancellationToken ct = default)
    {
        var path = Resolve(name);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string name, CancellationToken ct = default)
    {
        File.Delete(Resolve(name));
        return Task.CompletedTask;
    }

    // Keeps names like "../../etc/passwd" from escaping the uploads folder.
    private string Resolve(string name)
    {
        var path = Path.GetFullPath(Path.Combine(_root, name));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar))
            throw new ArgumentException($"Invalid file name: {name}");
        return path;
    }
}
