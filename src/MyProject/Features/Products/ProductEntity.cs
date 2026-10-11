using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using MyProject.Shared;

namespace MyProject.Features.Products;

[Table("products")]
public class ProductEntity : BaseEntity
{
    [MaxLength(200)]
    public string Name { get; set; } = "";

    [Precision(18, 2)]
    public decimal Price { get; set; }

    public string? ImageKey { get; set; }
}
