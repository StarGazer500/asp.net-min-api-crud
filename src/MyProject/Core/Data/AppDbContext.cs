using Microsoft.EntityFrameworkCore;
using MyProject.Core.Data.Entities;

namespace MyProject.Core.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
}
