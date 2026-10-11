namespace MyProject.Features.Products;

public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProducts(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        group.MapGet("/", async (ProductService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(ct)));

        group.MapGet("/{id:int}", async (int id, ProductService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, ct)))
            .WithName("GetProduct");

        group.MapPost("/", async (CreateProductDto dto, ProductService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(dto, ct);
            return TypedResults.CreatedAtRoute(created, "GetProduct", new { id = created.Id });
        });

        group.MapPost("/{id:int}/image", async (int id, IFormFile file, ProductService service, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            return TypedResults.Ok(await service.UploadImageAsync(id, stream, file.FileName, ct));
        })
        .DisableAntiforgery();   // required for IFormFile in minimal APIs (.NET 8+); secure this route with auth

        group.MapDelete("/{id:int}", async (int id, ProductService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        return app;
    }
}
