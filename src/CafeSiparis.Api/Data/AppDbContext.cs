using CafeSiparis.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CafeSiparis.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DiningTable> Tables => Set<DiningTable>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<TableAccount> Accounts => Set<TableAccount>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DiningTable>().HasIndex(t => new { t.Section, t.Number }).IsUnique();
        modelBuilder.Entity<Product>().Property(p => p.Price).HasPrecision(10, 2);
        modelBuilder.Entity<OrderItem>().Property(i => i.UnitPrice).HasPrecision(10, 2);
        modelBuilder.Entity<TableAccount>().Property(a => a.PaidAmount).HasPrecision(10, 2);
        modelBuilder.Entity<Payment>().Property(p => p.Amount).HasPrecision(10, 2);
    }
}
