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
    public async Task CreateAsync_returns_dto_with_new_id()
    {
        var dto = await _service.CreateAsync(new CreateProductDto("Keyboard", 49.99m));

        Assert.Equal(1, dto.Id);
        Assert.Equal("Keyboard", dto.Name);
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
        var created = await _service.CreateAsync(new CreateProductDto("Mouse", 19m));

        var dto = await _service.UploadImageAsync(created.Id, new MemoryStream([1, 2, 3]), "photo.png");

        Assert.Equal($"products/{created.Id}/photo.png", dto.ImageKey);
        Assert.True(_storage.Files.ContainsKey(dto.ImageKey!));
    }

    [Fact]
    public async Task DeleteAsync_removes_product()
    {
        var created = await _service.CreateAsync(new CreateProductDto("Monitor", 199m));

        await _service.DeleteAsync(created.Id);

        Assert.Empty(_db.Products);
    }
}
