using Microsoft.EntityFrameworkCore;
using MyProject.Core.Data;
using MyProject.Core.Data.Entities;
using MyProject.Shared.Abstractions;
using MyProject.Shared.Exceptions;

namespace MyProject.Features.Products;

public class ProductService(AppDbContext db, IFileStorage storage)
{
    public async Task<ProductDto> GetAsync(int id, CancellationToken ct = default) =>
        ToDto(await FindAsync(id, ct));

    public Task<List<ProductDto>> ListAsync(CancellationToken ct = default) =>
        db.Products.AsNoTracking()
            .Select(p => new ProductDto(p.Id, p.Name, p.Price, p.ImageKey))
            .ToListAsync(ct);

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
    {
        var product = new Product { Name = dto.Name, Price = dto.Price };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    public async Task<ProductDto> UploadImageAsync(int id, Stream image, string fileName, CancellationToken ct = default)
    {
        var product = await FindAsync(id, ct);

        product.ImageKey = await storage.UploadAsync(image, $"products/{id}/{fileName}", ct);
        product.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        db.Products.Remove(await FindAsync(id, ct));
        await db.SaveChangesAsync(ct);
    }

    private async Task<Product> FindAsync(int id, CancellationToken ct) =>
        await db.Products.FindAsync([id], ct)
            ?? throw new NotFoundException($"Product {id} was not found.");

    private static ProductDto ToDto(Product p) => new(p.Id, p.Name, p.Price, p.ImageKey);
}
