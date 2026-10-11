using Microsoft.EntityFrameworkCore;
using MyProject.Core.Data;
using MyProject.Shared.Abstractions;
using MyProject.Shared.Exceptions;

namespace MyProject.Features.Products;

public class ProductService(AppDbContext db, IFileStorage storage)
{
    public async Task<ProductResponse> GetAsync(int id, CancellationToken ct = default) =>
        ToResponse(await FindAsync(id, ct));

    public Task<List<ProductResponse>> ListAsync(CancellationToken ct = default) =>
        db.Products.AsNoTracking()
            .Select(p => ToResponse(p))
            .ToListAsync(ct);

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var product = new ProductEntity { Name = request.Name, Price = request.Price };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return ToResponse(product);
    }

    public async Task<ProductResponse> UploadImageAsync(int id, Stream image, string fileName, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);

        product.ImageKey = await storage.UploadAsync(image, $"products/{id}/{fileName}", ct);
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToResponse(product);
    }

    public async Task<(Stream Content, string FileName)> GetImageAsync(int id, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);
        var image = product.ImageKey is null ? null : await storage.DownloadAsync(product.ImageKey, ct);

        return image is null
            ? throw new NotFoundException($"Product {id} has no image.")
            : (image, Path.GetFileName(product.ImageKey!));
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        db.Products.Remove(await FindAsync(id, ct));
        await db.SaveChangesAsync(ct);
    }

    private async Task<ProductEntity> FindAsync(int id, CancellationToken ct) =>
        await db.Products.FindAsync([id], ct)
            ?? throw new NotFoundException($"Product {id} was not found.");

    // ImageUrl points at the GET /api/products/{id}/image endpoint, never at the internal storage key.
    private static ProductResponse ToResponse(ProductEntity p) =>
        new(p.Id, p.Name, p.Price, p.ImageKey is null ? null : $"/api/products/{p.Id}/image");
}
