using System.ComponentModel.DataAnnotations;

namespace MyProject.Features.Products;

// Requests (input)

public record CreateProductRequest(
    [Required, MaxLength(200)] string Name,
    [Range(0, double.MaxValue, ErrorMessage = "Price must be 0 or more.")] decimal Price);

// Responses (output)

public record ProductResponse(int Id, string Name, decimal Price, string? ImageUrl);
