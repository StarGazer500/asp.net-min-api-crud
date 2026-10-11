using Microsoft.EntityFrameworkCore;
using MyProject.Core.Data;
using MyProject.Features.Products;
using MyProject.Shared.Exceptions;
using MyProject.Tests.Fakes;

namespace MyProject.Tests.Features.Products;

public class ProductServiceTests
{
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())   // fresh database per test
        .Options);
    private readonly InMemoryFileStorage _storage = new();
    private readonly ProductService _service;

    public ProductServiceTests() => _service = new ProductService(_db, _storage);

    [Fact]
    public async Task CreateAsync_returns_response_with_new_id()
    {
        var response = await _service.CreateAsync(new CreateProductRequest("Keyboard", 49.99m));

        Assert.Equal(1, response.Id);
        Assert.Equal("Keyboard", response.Name);
        Assert.Single(_db.Products);
    }

    [Fact]
    public async Task GetAsync_throws_NotFound_for_missing_product()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(42));
    }

    [Fact]
    public async Task UploadImageAsync_stores_file_and_sets_image_key()
    {
        var created = await _service.CreateAsync(new CreateProductRequest("Mouse", 19m));

        var response = await _service.UploadImageAsync(created.Id, new MemoryStream([1, 2, 3]), "photo.png");

        Assert.Equal($"/api/products/{created.Id}/image", response.ImageUrl);
        Assert.True(_storage.Files.ContainsKey($"products/{created.Id}/photo.png"));
    }

    [Fact]
    public async Task GetImageAsync_returns_uploaded_file()
    {
        var created = await _service.CreateAsync(new CreateProductRequest("Mouse", 19m));
        await _service.UploadImageAsync(created.Id, new MemoryStream([1, 2, 3]), "photo.png");

        var (content, fileName) = await _service.GetImageAsync(created.Id);

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        Assert.Equal([1, 2, 3], ms.ToArray());
        Assert.Equal("photo.png", fileName);
    }

    [Fact]
    public async Task GetImageAsync_throws_NotFound_when_product_has_no_image()
    {
        var created = await _service.CreateAsync(new CreateProductRequest("Mouse", 19m));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetImageAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_removes_product()
    {
        var created = await _service.CreateAsync(new CreateProductRequest("Monitor", 199m));

        await _service.DeleteAsync(created.Id);

        Assert.Empty(_db.Products);
    }
}
