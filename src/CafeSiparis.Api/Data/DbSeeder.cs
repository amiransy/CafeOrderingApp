using CafeSiparis.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CafeSiparis.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        var sections = new[] { "Sol", "Sağ", "Dış", "İç", "Kapı" };
        var existingTables = await db.Tables.ToListAsync();

        if (existingTables.Count != 50 || existingTables.Any(t => t.Section == "Genel"))
        {
            // Reset to new 50 tables across 5 sections
            if (existingTables.Count > 0)
            {
                db.Tables.RemoveRange(existingTables);
                await db.SaveChangesAsync();
            }

            foreach (var sec in sections)
            {
                for (int i = 1; i <= 10; i++)
                {
                    db.Tables.Add(new DiningTable
                    {
                        Number = i,
                        Name = $"{sec} {i}",
                        Section = sec
                    });
                }
            }
            await db.SaveChangesAsync();
        }

        if (!await db.Categories.AnyAsync())
        {
            var sicak = new Category { Name = "Sıcak İçecekler", SortOrder = 1 };
            var soguk = new Category { Name = "Soğuk İçecekler", SortOrder = 2 };
            var tatlilar = new Category { Name = "Tatlılar", SortOrder = 3 };
            var yemekler = new Category { Name = "Yiyecekler", SortOrder = 4 };

            db.Categories.AddRange(sicak, soguk, tatlilar, yemekler);
            await db.SaveChangesAsync();

            db.Products.AddRange(
                new Product { CategoryId = sicak.Id, Name = "Türk Kahvesi", Price = 90 },
                new Product { CategoryId = sicak.Id, Name = "Espresso", Price = 85 },
                new Product { CategoryId = sicak.Id, Name = "Latte", Price = 120 },
                new Product { CategoryId = sicak.Id, Name = "Filtre Kahve", Price = 100 },
                new Product { CategoryId = sicak.Id, Name = "Çay", Price = 40 },
                new Product { CategoryId = sicak.Id, Name = "Salep", Price = 95 },
                new Product { CategoryId = soguk.Id, Name = "Limonata", Price = 80 },
                new Product { CategoryId = soguk.Id, Name = "Ayran", Price = 45 },
                new Product { CategoryId = soguk.Id, Name = "Kola", Price = 60 },
                new Product { CategoryId = soguk.Id, Name = "Ice Latte", Price = 130 },
                new Product { CategoryId = tatlilar.Id, Name = "Baklava", Price = 180 },
                new Product { CategoryId = tatlilar.Id, Name = "Cheesecake", Price = 160 },
                new Product { CategoryId = tatlilar.Id, Name = "Sütlaç", Price = 110 },
                new Product { CategoryId = yemekler.Id, Name = "Karışık Tost", Price = 150 },
                new Product { CategoryId = yemekler.Id, Name = "Menemen", Price = 170 },
                new Product { CategoryId = yemekler.Id, Name = "Omlet", Price = 155 }
            );
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Accounts ADD COLUMN OpenedBy TEXT NOT NULL DEFAULT 'Garson';");
        }
        catch { }

        try
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Products ADD COLUMN IsFeatured INTEGER NOT NULL DEFAULT 0;");
        }
        catch { }

        var featuredNames = new[] { "Çay", "Türk Kahvesi", "Filtre Kahve", "Limonata", "Latte", "Ayran", "Cheesecake", "Karışık Tost" };
        var featuredProducts = await db.Products.Where(p => featuredNames.Contains(p.Name)).ToListAsync();
        foreach (var p in featuredProducts)
        {
            p.IsFeatured = true;
        }

        await db.SaveChangesAsync();
    }
}
