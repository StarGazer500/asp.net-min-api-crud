# MyProject: ASP.NET Core Minimal API

Feature-based architecture with Ports and Adapters, using **Minimal APIs** as the routing layer.

## 1. Overview

- **Features** (`Features/`) group code by business capability (Products, Orders, ...). Each feature owns its endpoints, service and DTOs.
- **Ports** (`Shared/Abstractions/`) are interfaces for external systems (file storage, email).
- **Adapters** (`Adapters/`) implement the ports (S3, SMTP). Features never reference them.
- **Core** (`Core/`) is app plumbing: config, DbContext and entities, logging, error handling.
- **Shared** (`Shared/`) holds ports, base types and exceptions used by several features.

```
Features --> Shared/Abstractions <-- Adapters
```

## 2. Tech stack

| Concern | Choice |
|---|---|
| Runtime | .NET 10 or later |
| API style | Minimal APIs with `MapGroup` |
| Database | PostgreSQL via EF Core (Npgsql), `DbContext` used directly (no repositories) |
| Validation | Built-in (`AddValidation()` + DataAnnotations attributes) |
| File storage | AWS S3 (adapter) |
| Email | SMTP (adapter) |
| API docs | OpenAPI (built in) |

## 3. Prerequisites

- .NET SDK 10+ (`dotnet --version`)
- PostgreSQL running locally (or Docker)
- VS Code with the **C# Dev Kit** extension
- EF Core CLI: `dotnet tool install --global dotnet-ef`

## 4. Create the project

```bash
mkdir MyProject && cd MyProject
dotnet new webapi -n MyProject -o src/MyProject      # minimal API is the default template
dotnet new sln -n MyProject
dotnet sln add src/MyProject

cd src/MyProject
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package AWSSDK.S3
dotnet add package AWSSDK.Extensions.NETCore.Setup
```

Create the folders (delete the template's sample weather endpoint first):

```bash
mkdir -p Adapters/S3 Adapters/Email Adapters/Local \
         Features/Products \
         Core/Config Core/Data/Entities Core/Errors Core/Logging \
         Shared/Abstractions Shared/Exceptions
```

## 5. Folder structure

```
src/MyProject/
  Adapters/
    S3/                    S3FileStorage.cs, StorageOptions.cs
    Email/                 SmtpEmailSender.cs
    Local/                 LocalFileStorage.cs   (Development stand-in for S3)
  Features/
    Products/
      ProductsEndpoints.cs     (the "router")
      ProductService.cs
      ProductDto.cs            (request/response DTOs + validation attributes)
    Orders/
      ...
  Core/
    Config/
    Data/                  AppDbContext.cs
      Entities/            Product.cs, ...
      Migrations/
    Errors/                GlobalExceptionHandler.cs
    Logging/
  Shared/
    Abstractions/          IFileStorage.cs, IEmailSender.cs
    Exceptions/            AppExceptions.cs
    BaseEntity.cs
  Program.cs
  appsettings.json
```

## 6. Layer responsibilities

| Folder | Contains | Test |
|---|---|---|
| `Features/` | Router, service and DTOs per business capability | "Is this specific to one business capability?" |
| `Shared/` | Ports (interfaces), base entity, exceptions | "Do several features depend on this?" |
| `Adapters/` | Implementations of ports for external systems | "Could I swap this for another provider or a fake?" |
| `Core/` | Config, DbContext, entities, logging, error handling | "Does the app need this to run, regardless of vendor?" |

## 7. Dependency rules

1. Features depend on **interfaces in `Shared/Abstractions`**, never on anything in `Adapters/`.
2. Adapters implement ports and never reference features.
3. A feature only writes to its own tables. Cross-feature changes go through the other feature's service (or a small interface it exposes).
4. Routers (controllers/endpoints) only parse the request, call a service, and map the result to HTTP. No business logic, no `DbContext` calls.
5. API responses use **DTOs**, never entities.
6. Interfaces live where the consumer can see them, never next to the adapter implementation.
7. Interfaces describe what the app needs, not what the vendor offers. No SDK types in an interface.

## 8. Implementation guide

### 8.1 Configuration (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=myproject;Username=postgres;Password=postgres"
  },
  "Storage": {
    "Bucket": "my-bucket"
  },
  "Email": {
    "Host": "localhost",
    "Port": 25,
    "From": "no-reply@myproject.local"
  }
}
```

Keep real secrets out of source control. Use `dotnet user-secrets` locally.

### 8.2 Shared

```csharp
// Shared/BaseEntity.cs
namespace MyProject.Shared;

public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
```

```csharp
// Shared/Abstractions/IFileStorage.cs
namespace MyProject.Shared.Abstractions;

public interface IFileStorage
{
    Task<string> UploadAsync(Stream file, string name, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string name, CancellationToken ct = default);
    Task DeleteAsync(string name, CancellationToken ct = default);
}
```

```csharp
// Shared/Abstractions/IEmailSender.cs
namespace MyProject.Shared.Abstractions;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
```

```csharp
// Shared/Exceptions/AppExceptions.cs
namespace MyProject.Shared.Exceptions;

public abstract class AppException(string message) : Exception(message);
public class NotFoundException(string message) : AppException(message);
public class ConflictException(string message) : AppException(message);
public class StorageException(string message) : AppException(message);
```

### 8.3 Core

```csharp
// Core/Data/Entities/Product.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using MyProject.Shared;

namespace MyProject.Core.Data.Entities;

[Table("products")]
public class Product : BaseEntity
{
    [MaxLength(200)]
    public string Name { get; set; } = "";

    [Precision(18, 2)]
    public decimal Price { get; set; }

    public string? ImageKey { get; set; }
}
```

Table and column settings are attributes on the entity. A non-nullable `string` is already `NOT NULL`, so `Name` needs no `[Required]`.

```csharp
// Core/Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using MyProject.Core.Data.Entities;

namespace MyProject.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}
```

Add one `DbSet` per entity. EF Core only maps entities it can reach from the `DbContext`.

```csharp
// Core/Errors/GlobalExceptionHandler.cs
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyProject.Shared.Exceptions;

namespace MyProject.Core.Errors;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Server error")
        };

        if (status == 500) logger.LogError(exception, "Unhandled exception");

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == 500 ? null : exception.Message
        }, ct);

        return true;
    }
}
```

The database and error handler are registered in `Program.cs` (see 8.7).

### 8.4 Adapters

```csharp
// Adapters/S3/StorageOptions.cs
namespace MyProject.Adapters.S3;

public class StorageOptions
{
    public string Bucket { get; set; } = "";
}
```

```csharp
// Adapters/S3/S3FileStorage.cs
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using MyProject.Shared.Abstractions;
using MyProject.Shared.Exceptions;

namespace MyProject.Adapters.S3;

internal class S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _bucket = options.Value.Bucket;

    public async Task<string> UploadAsync(Stream file, string name, CancellationToken ct = default)
    {
        try
        {
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket, Key = name, InputStream = file
            }, ct);
            return name;
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageException($"Upload failed: {ex.Message}");   // translate vendor errors
        }
    }

    public async Task<Stream?> DownloadAsync(string name, CancellationToken ct = default)
    {
        try
        {
            var res = await s3.GetObjectAsync(_bucket, name, ct);
            return res.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default) =>
        await s3.DeleteObjectAsync(_bucket, name, ct);
}
```

```csharp
// Adapters/Email/SmtpEmailSender.cs
using System.Net.Mail;
using MyProject.Shared.Abstractions;

namespace MyProject.Adapters.Email;

internal class SmtpEmailSender(IConfiguration config) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        using var client = new SmtpClient(config["Email:Host"], config.GetValue<int>("Email:Port"));
        var message = new MailMessage(config["Email:From"]!, to, subject, body);
        await client.SendMailAsync(message, ct);
    }
}
```

For local development, a disk-based adapter stands in for S3 so you don't need AWS credentials:

```csharp
// Adapters/Local/LocalFileStorage.cs
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
```

`Program.cs` picks the adapter (see 8.7). Swapping providers is a one-line change there, for example `AddScoped<IFileStorage, AzureBlobFileStorage>()`.

### 8.5 Feature: Products (service)

The service uses `AppDbContext` directly. EF Core's `DbContext` already is a repository and unit of work, so a wrapper adds code without adding a seam you need.

```csharp
// Features/Products/ProductService.cs
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
```

### 8.6 Feature: Products (DTOs, validation, router)

```csharp
// Features/Products/ProductDto.cs
using System.ComponentModel.DataAnnotations;

namespace MyProject.Features.Products;

public record ProductDto(int Id, string Name, decimal Price, string? ImageKey);

public record CreateProductDto(
    [Required, MaxLength(200)] string Name,
    [Range(0, double.MaxValue, ErrorMessage = "Price must be 0 or more.")] decimal Price);
```

Validation is built into .NET 10. Call `builder.Services.AddValidation()` once in `Program.cs`, and every endpoint parameter with DataAnnotations attributes is checked before the handler runs. Invalid input returns a 400 `ValidationProblem` with the errors per field. No filter or extra package is needed.

```csharp
// Features/Products/ProductsEndpoints.cs   <- the "router"
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
```

The service throws `NotFoundException`, and the global handler turns it into a 404, so endpoints need no null checks or try/catch.

### 8.7 Program.cs

All services are registered here with the standard `builder.Services.Add...` calls:

```csharp
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using MyProject.Adapters.Email;
using MyProject.Adapters.Local;
using MyProject.Adapters.S3;
using MyProject.Core.Data;
using MyProject.Core.Errors;
using MyProject.Features.Products;
using MyProject.Shared.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddValidation();   // built-in checks for [Required], [MaxLength] etc. on endpoint parameters

// Database
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Errors
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// File storage: local disk in Development, S3 otherwise
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
}
else
{
    builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
    builder.Services.AddAWSService<IAmazonS3>();
    builder.Services.AddScoped<IFileStorage, S3FileStorage>();
}

// Email
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// Features
builder.Services.AddScoped<ProductService>();

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();

app.MapProducts();
// app.MapOrders();

app.Run();

public partial class Program { }
```

Each feature adds two lines to `Program.cs`: `AddScoped<XService>()` and `MapX()`.

### 8.8 Minimal API specific tips

- Use `MapGroup` per feature so every route shares a prefix, tags, auth policy and filters.
- Prefer `TypedResults` over `Results` for compile-time-checked return types and better OpenAPI output.
- Keep each endpoint lambda to a few lines. If it grows, extract a private static method in the same file.
- Apply auth at the group level: `group.RequireAuthorization()`.
- Add filters (logging, custom checks) with `AddEndpointFilter` on the group or on individual endpoints. Validation needs no filter; `AddValidation()` handles it.

## 9. Database migrations

Run from `src/MyProject`:

```bash
dotnet ef migrations add InitialCreate --output-dir Core/Data/Migrations
dotnet ef database update
```

After changing any entity or configuration:

```bash
dotnet ef migrations add <DescriptiveName> --output-dir Core/Data/Migrations
dotnet ef database update
```

## 10. Run and test the API

```bash
dotnet watch run
```

Quick check (`dotnet watch run` uses the `http` profile in `Properties/launchSettings.json`, port 5280; check the console output if yours differs):

```bash
curl -X POST http://localhost:5280/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Keyboard","price":49.99}'

curl http://localhost:5280/api/products
```

In VS Code, install the **REST Client** extension and keep requests in a `requests.http` file, or use the built-in OpenAPI document at `/openapi/v1.json`.

## 11. Adding a new feature (checklist)

1. Add `Core/Data/Entities/<Name>.cs` (inheriting `BaseEntity`, with attributes like `[MaxLength]`) and a `DbSet<<Name>>` in `AppDbContext`.
2. Create `Features/<Name>s/`.
3. Add `<Name>Service` using `AppDbContext`, with the business logic. Throw exceptions from `Shared/Exceptions`.
4. Add DTOs with validation attributes. Never return entities.
5. Add the router file (`<Name>sEndpoints.cs` with `Map<Name>s()`).
6. Register the service and map the endpoints in `Program.cs`.
7. Add a migration and update the database.
8. Add service unit tests with EF Core's in-memory database and fakes of the ports.

## 12. Testing

Ports like `IFileStorage` get hand-written fakes, so unit tests need no mocking library or network:

```csharp
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
```

Suggested test projects:

```bash
dotnet new xunit -n MyProject.Tests -o tests/MyProject.Tests
dotnet sln add tests/MyProject.Tests
dotnet add tests/MyProject.Tests reference src/MyProject
```

- **Unit tests:** services with EF Core's in-memory database (`dotnet add package Microsoft.EntityFrameworkCore.InMemory`) and fake ports:

```csharp
// Shortened from tests/MyProject.Tests/Features/Products/ProductServiceTests.cs
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
        Assert.Single(_db.Products);
    }
}
```

  The in-memory provider doesn't enforce database constraints (max length, foreign keys), so cover those with integration tests.
- **Integration tests:** `WebApplicationFactory<Program>` with a Testcontainers PostgreSQL instance. Add `public partial class Program { }` at the bottom of `Program.cs` so the test project can see it.

## 13. Enforcing the architecture

In one project these rules are conventions. Enforce them with an architecture test (`NetArchTest.Rules`):

```csharp
[Fact]
public void Features_do_not_depend_on_adapters_or_vendor_sdks()
{
    var result = Types.InAssembly(typeof(Program).Assembly)
        .That().ResideInNamespace("MyProject.Features")
        .ShouldNot().HaveDependencyOnAny("MyProject.Adapters", "Amazon.S3")
        .GetResult();

    Assert.True(result.IsSuccessful);
}
```

When the project grows, promote `Adapters`, `Core` and `Shared` to class libraries. `Adapters` references only `Shared`, so the compiler blocks features from touching vendor code. Later, any feature can become its own module project with a contracts library.

## 14. Conventions

- Folders and namespaces mirror each other (`MyProject.Features.Products`, `MyProject.Core.Data.Entities`).
- Async methods end in `Async` and accept a `CancellationToken`.
- One public type per file, named after the file.
- Adapters are `internal`; services and routers are `public`.
- No business logic in routers, no HTTP concerns in services.
- Do not wrap things that are already abstractions (`ILogger`) or that you will never swap.
