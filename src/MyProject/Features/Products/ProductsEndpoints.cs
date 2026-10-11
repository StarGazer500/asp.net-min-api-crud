using Microsoft.AspNetCore.StaticFiles;

namespace MyProject.Features.Products;

public static class ProductsEndpoints
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static IEndpointRouteBuilder MapProducts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        group.MapGet("/", async (ProductService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(ct)));

        group.MapGet("/{id:int}", async (int id, ProductService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, ct)))
            .WithName("GetProduct");

        group.MapPost("/", async (CreateProductRequest request, ProductService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return TypedResults.CreatedAtRoute(created, "GetProduct", new { id = created.Id });
        });

        group.MapPost("/{id:int}/image", async (int id, IFormFile file, ProductService service, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            return TypedResults.Ok(await service.UploadImageAsync(id, stream, file.FileName, ct));
        })
        .DisableAntiforgery();   // required for IFormFile in minimal APIs (.NET 8+); secure this route with auth

        group.MapGet("/{id:int}/image", async (int id, ProductService service, CancellationToken ct) =>
        {
            var (content, fileName) = await service.GetImageAsync(id, ct);
            var contentType = ContentTypes.TryGetContentType(fileName, out var type) ? type : "application/octet-stream";
            return TypedResults.Stream(content, contentType);
        });

        group.MapDelete("/{id:int}", async (int id, ProductService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
