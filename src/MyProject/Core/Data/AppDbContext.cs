using Microsoft.EntityFrameworkCore;
using MyProject.Features.Products;

namespace MyProject.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
}
