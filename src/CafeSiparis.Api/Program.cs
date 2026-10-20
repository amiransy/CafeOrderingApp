using CafeSiparis.Api;
using CafeSiparis.Api.Data;
using CafeSiparis.Api.Hubs;
using CafeSiparis.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://*:5088");

builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true).AllowCredentials());
});

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "cafe.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db);
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHub<KitchenHub>("/hubs/kitchen");
app.MapGet("/", () => Results.Redirect("/station/"));
app.MapGet("/api/health", () => Results.Ok(new { ok = true }));

// 1. Masalar ve Durumları
app.MapGet("/api/tables", async (AppDbContext db) =>
{
    var sectionOrder = new Dictionary<string, int>
    {
        ["Sol"] = 1,
        ["Sağ"] = 2,
        ["Dış"] = 3,
        ["İç"] = 4,
        ["Kapı"] = 5
    };

    var tables = await db.Tables
        .Include(t => t.Accounts.Where(a => !a.IsClosed))
            .ThenInclude(a => a.Orders)
                .ThenInclude(o => o.Items.Where(i => !i.IsCancelled))
        .Include(t => t.Accounts.Where(a => !a.IsClosed))
            .ThenInclude(a => a.Payments)
        .ToListAsync();

    var sortedTables = tables
        .OrderBy(t => sectionOrder.GetValueOrDefault(t.Section, 99))
        .ThenBy(t => t.Number)
        .ToList();

    var result = sortedTables.Select(t =>
    {
        var activeAccount = t.Accounts.FirstOrDefault(a => !a.IsClosed);
        if (activeAccount == null)
        {
            return new TableStatusDto(
                t.Id,
                t.Number,
                t.Name,
                t.Section,
                IsOccupied: false,
                ActiveAccountId: null,
                TotalAmount: 0,
                PaidAmount: 0,
                RemainingAmount: 0,
                OpenedAt: null,
                ItemCount: 0);
        }

        var total = activeAccount.Orders.SelectMany(o => o.Items).Sum(i => i.UnitPrice * i.Quantity);
        var paid = activeAccount.PaidAmount;
        var remaining = Math.Max(0, total - paid);
        var itemCount = activeAccount.Orders.SelectMany(o => o.Items).Sum(i => i.Quantity);

        return new TableStatusDto(
            t.Id,
            t.Number,
            t.Name,
            t.Section,
            IsOccupied: true,
            ActiveAccountId: activeAccount.Id,
            TotalAmount: total,
            PaidAmount: paid,
            RemainingAmount: remaining,
            OpenedAt: activeAccount.OpenedAt,
            ItemCount: itemCount);
    }).ToList();

    return Results.Ok(result);
});

// 2. Masa Aktif Hesap Detayı
app.MapGet("/api/tables/{id:int}/account", async (int id, AppDbContext db) =>
{
    var table = await db.Tables.FindAsync(id);
    if (table is null) return Results.NotFound("Masa bulunamadı.");

    var account = await db.Accounts
        .Include(a => a.Orders)
            .ThenInclude(o => o.Items)
        .Include(a => a.Payments)
        .FirstOrDefaultAsync(a => a.TableId == id && !a.IsClosed);

    if (account is null)
    {
        return Results.Ok(new TableAccountDetailDto(
            AccountId: 0,
            TableId: table.Id,
            TableNumber: table.Number,
            TableName: table.Name,
            Section: table.Section,
            OpenedAt: DateTime.Now,
            OpenedBy: "",
            TotalAmount: 0,
            PaidAmount: 0,
            RemainingAmount: 0,
            Items: [],
            Payments: []));
    }

    var items = account.Orders
        .SelectMany(o => o.Items.Select(i => new OrderItemDetailDto(
            i.Id,
            i.OrderId,
            i.ProductId,
            i.ProductName,
            i.UnitPrice,
            i.Quantity,
            i.PaidQuantity,
            Math.Max(0, i.Quantity - i.PaidQuantity),
            i.UnitPrice * i.Quantity,
            o.CreatedAt,
            o.CreatedBy,
            i.IsCancelled)))
        .ToList();

    var payments = account.Payments
        .OrderByDescending(p => p.PaidAt)
        .Select(p => new PaymentDto(p.Id, p.Amount, p.PaymentMethod, p.PaidAt, p.Note))
        .ToList();

    var total = items.Where(i => !i.IsCancelled).Sum(i => i.LineTotal);
    var remaining = Math.Max(0, total - account.PaidAmount);

    return Results.Ok(new TableAccountDetailDto(
        account.Id,
        table.Id,
        table.Number,
        table.Name,
        table.Section,
        account.OpenedAt,
        account.OpenedBy,
        total,
        account.PaidAmount,
        remaining,
        items,
        payments));
});

// 3. Menü
app.MapGet("/api/menu", async (AppDbContext db) =>
    await db.Categories
        .OrderBy(c => c.SortOrder)
        .Select(c => new CategoryDto(
            c.Id,
            c.Name,
            c.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .Select(p => new ProductDto(p.Id, p.Name, p.Price, p.IsFeatured))
                .ToList()))
        .ToListAsync());

// 4. Bekleyen Yazdırılmamış Mutfak/Bar Fişleri
app.MapGet("/api/orders/pending", async (AppDbContext db) =>
{
    var orders = await db.Orders
        .Include(o => o.Table)
        .Include(o => o.Account)
        .Include(o => o.Items)
        .Where(o => o.Status == "Yeni")
        .OrderBy(o => o.CreatedAt)
        .ToListAsync();
    return orders.Select(ToTicket);
});

// 5. Sipariş Ekle (Garson veya Kasa)
app.MapPost("/api/orders", async (CreateOrderDto dto, AppDbContext db, IHubContext<KitchenHub> hub) =>
{
    if (dto.Items is not { Count: > 0 })
    {
        return Results.BadRequest("Siparişte en az bir ürün olmalı.");
    }

    var table = await db.Tables.FindAsync(dto.TableId);
    if (table is null)
    {
        return Results.NotFound("Masa bulunamadı.");
    }

    var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
    var products = await db.Products.Where(p => productIds.Contains(p.Id) && p.IsActive).ToListAsync();
    if (products.Count != productIds.Count)
    {
        return Results.BadRequest("Menüde olmayan bir ürün seçildi.");
    }

    // Aktif açık masa hesabı var mı? Yoksa yeni hesap aç.
    var account = await db.Accounts
        .FirstOrDefaultAsync(a => a.TableId == table.Id && !a.IsClosed);

    var senderName = string.IsNullOrWhiteSpace(dto.CreatedBy) ? "Garson" : dto.CreatedBy.Trim();

    if (account is null)
    {
        account = new TableAccount
        {
            TableId = table.Id,
            OpenedAt = DateTime.Now,
            OpenedBy = senderName,
            IsClosed = false,
            PaidAmount = 0
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
    }

    var order = new Order
    {
        AccountId = account.Id,
        TableId = table.Id,
        CreatedAt = DateTime.Now,
        Status = "Yeni",
        Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
        CreatedBy = senderName
    };

    foreach (var item in dto.Items)
    {
        if (item.Quantity < 1) continue;
        var product = products.First(p => p.Id == item.ProductId);
        order.Items.Add(new OrderItem
        {
            ProductId = product.Id,
            ProductName = product.Name,
            UnitPrice = product.Price,
            Quantity = item.Quantity,
            PaidQuantity = 0,
            IsCancelled = false
        });
    }

    if (order.Items.Count == 0)
    {
        return Results.BadRequest("Geçerli ürün adedi bulunamadı.");
    }

    db.Orders.Add(order);
    await db.SaveChangesAsync();

    await db.Entry(order).Reference(o => o.Table).LoadAsync();
    order.Account = account;
    var ticket = ToTicket(order);

    // Mutfak/bar yazıcısına yeni sipariş fişini fırlat
    await hub.Clients.All.SendAsync("orderReceived", ticket);
    // Masalar ekranını canlı güncelle
    await hub.Clients.All.SendAsync("tablesUpdated");

    return Results.Created($"/api/orders/{order.Id}", ticket);
});

// 6. Fiş Yazdırıldı Olarak İşaretle
app.MapPost("/api/orders/{id:int}/printed", async (int id, AppDbContext db) =>
{
    var order = await db.Orders.FindAsync(id);
    if (order is null) return Results.NotFound();
    order.Status = "Yazdirildi";
    await db.SaveChangesAsync();
    return Results.Ok();
});

// 7. Kasadan Ürün Sil / İptal Et
app.MapDelete("/api/order-items/{id:int}", async (int id, AppDbContext db, IHubContext<KitchenHub> hub) =>
{
    var item = await db.OrderItems
        .Include(i => i.Order)
        .FirstOrDefaultAsync(i => i.Id == id);

    if (item is null) return Results.NotFound("Ürün kaydı bulunamadı.");

    var account = await db.Accounts
        .FirstOrDefaultAsync(a => a.Id == item.Order.AccountId && !a.IsClosed);

    if (account is null) return Results.BadRequest("Kapanmış hesaptaki ürünler silinemez.");

    // Eğer adeti 1'den büyükse 1 azalt, 1 ise tamamen sil
    if (item.Quantity > 1)
    {
        item.Quantity--;
    }
    else
    {
        db.OrderItems.Remove(item);
    }

    await db.SaveChangesAsync();
    await hub.Clients.All.SendAsync("tablesUpdated");
    return Results.Ok(new { success = true });
});

// 8. Masa Aktarma (Masa Değiştir)
app.MapPost("/api/tables/{id:int}/transfer", async (int id, TransferTableRequest dto, AppDbContext db, IHubContext<KitchenHub> hub) =>
{
    if (id == dto.TargetTableId) return Results.BadRequest("Aynı masaya aktarma yapılamaz.");

    var sourceTable = await db.Tables.FindAsync(id);
    var targetTable = await db.Tables.FindAsync(dto.TargetTableId);
    if (sourceTable is null || targetTable is null) return Results.NotFound("Masa bulunamadı.");

    var sourceAccount = await db.Accounts
        .Include(a => a.Orders)
        .FirstOrDefaultAsync(a => a.TableId == id && !a.IsClosed);

    if (sourceAccount is null) return Results.BadRequest("Aktarılacak masanın açık hesabı yok.");

    var targetAccount = await db.Accounts
        .FirstOrDefaultAsync(a => a.TableId == dto.TargetTableId && !a.IsClosed);

    if (targetAccount is not null) return Results.BadRequest("Hedef masa dolu! Dolu masaya aktarma yapılamaz (birleştirme kullanın).");

    sourceAccount.TableId = targetTable.Id;
    foreach (var o in sourceAccount.Orders)
    {
        o.TableId = targetTable.Id;
    }

    await db.SaveChangesAsync();
    await hub.Clients.All.SendAsync("tablesUpdated");

    return Results.Ok(new { message = $"{sourceTable.Name} başarıyla {targetTable.Name} masasına aktarıldı." });
});

// 9. Masa Birleştirme
app.MapPost("/api/tables/{id:int}/merge", async (int id, MergeTableRequest dto, AppDbContext db, IHubContext<KitchenHub> hub) =>
{
    if (id == dto.TargetTableId) return Results.BadRequest("Aynı masa birleştirilemez.");

    var sourceTable = await db.Tables.FindAsync(id);
    var targetTable = await db.Tables.FindAsync(dto.TargetTableId);
    if (sourceTable is null || targetTable is null) return Results.NotFound("Masa bulunamadı.");

    var sourceAccount = await db.Accounts
        .Include(a => a.Orders)
        .Include(a => a.Payments)
        .FirstOrDefaultAsync(a => a.TableId == id && !a.IsClosed);

    var targetAccount = await db.Accounts
        .Include(a => a.Orders)
        .Include(a => a.Payments)
        .FirstOrDefaultAsync(a => a.TableId == dto.TargetTableId && !a.IsClosed);

    if (sourceAccount is null || targetAccount is null)
    {
        return Results.BadRequest("Birleştirme için her iki masanın da açık hesabı olmalıdır.");
    }

    // Kaynak masadaki tüm siparişleri hedef masaya devret
    foreach (var o in sourceAccount.Orders)
    {
        o.AccountId = targetAccount.Id;
        o.TableId = targetTable.Id;
    }

    // Varsa yapılan parçalı ödemeleri de hedef hesaba devret
    foreach (var p in sourceAccount.Payments)
    {
        p.AccountId = targetAccount.Id;
    }

    targetAccount.PaidAmount += sourceAccount.PaidAmount;

    // Kaynak hesabı kapat
    sourceAccount.IsClosed = true;
    sourceAccount.ClosedAt = DateTime.Now;
    sourceAccount.PaidAmount = 0;

    await db.SaveChangesAsync();
    await hub.Clients.All.SendAsync("tablesUpdated");

    return Results.Ok(new { message = $"{sourceTable.Name}, {targetTable.Name} ile başarıyla birleştirildi." });
});

// 10. Ödeme Al / Parçalı Ödeme / Hesabı Kapat
app.MapPost("/api/tables/{id:int}/pay", async (int id, PayAccountRequest dto, AppDbContext db, IHubContext<KitchenHub> hub) =>
{
    if (dto.Amount <= 0) return Results.BadRequest("Ödeme tutarı 0'dan büyük olmalıdır.");

    var table = await db.Tables.FindAsync(id);
    if (table is null) return Results.NotFound("Masa bulunamadı.");

    var account = await db.Accounts
        .Include(a => a.Orders)
            .ThenInclude(o => o.Items)
        .Include(a => a.Payments)
        .FirstOrDefaultAsync(a => a.TableId == id && !a.IsClosed);

    if (account is null) return Results.BadRequest("Masanın açık hesabı bulunmuyor.");

    // Ödeme kaydını oluştur
    var payment = new Payment
    {
        AccountId = account.Id,
        Amount = dto.Amount,
        PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "Nakit" : dto.PaymentMethod,
        PaidAt = DateTime.Now,
        Note = dto.Note
    };
    db.Payments.Add(payment);
    account.PaidAmount += dto.Amount;

    // Eğer belirli ürünler seçilerek ödenmişse parçalı ürün adetlerini güncelle
    if (dto.ItemsToPay is { Count: > 0 })
    {
        var allItems = account.Orders.SelectMany(o => o.Items).ToList();
        foreach (var reqItem in dto.ItemsToPay)
        {
            var match = allItems.FirstOrDefault(i => i.Id == reqItem.OrderItemId);
            if (match is not null)
            {
                match.PaidQuantity = Math.Min(match.Quantity, match.PaidQuantity + reqItem.Quantity);
            }
        }
    }

    var total = account.Orders
        .SelectMany(o => o.Items)
        .Where(i => !i.IsCancelled)
        .Sum(i => i.UnitPrice * i.Quantity);

    var remaining = Math.Max(0, total - account.PaidAmount);

    // Kalan 0 ise veya hesap kapatılmak istenmişse masayı kapat
    if (dto.CloseTableIfFullyPaid && remaining <= 0)
    {
        account.IsClosed = true;
        account.ClosedAt = DateTime.Now;
    }

    await db.SaveChangesAsync();
    await hub.Clients.All.SendAsync("tablesUpdated");

    return Results.Ok(new
    {
        success = true,
        paid = dto.Amount,
        total,
        remaining,
        isClosed = account.IsClosed
    });
});

// 11. Masayı Zorla Kapat (Masayı Boşalt)
app.MapPost("/api/tables/{id:int}/close", async (int id, AppDbContext db, IHubContext<KitchenHub> hub) =>
{
    var account = await db.Accounts
        .FirstOrDefaultAsync(a => a.TableId == id && !a.IsClosed);

    if (account is not null)
    {
        account.IsClosed = true;
        account.ClosedAt = DateTime.Now;
        await db.SaveChangesAsync();
        await hub.Clients.All.SendAsync("tablesUpdated");
    }

    return Results.Ok(new { success = true });
});

// 12. Yönetici PIN Doğrulama & Yönetimi
app.MapPost("/api/auth/verify-manager", (ManagerPinRequest dto) =>
{
    var validPin = Environment.GetEnvironmentVariable("CAFE_MANAGER_PIN") ?? "1234";
    if (dto.Pin == validPin)
    {
        return Results.Ok(new { success = true });
    }
    return Results.BadRequest(new { success = false, message = "Geçersiz Yönetici PIN kodu!" });
});

app.MapGet("/api/auth/manager-pin", () =>
{
    var pin = Environment.GetEnvironmentVariable("CAFE_MANAGER_PIN") ?? "1234";
    return Results.Ok(new { pin });
});

app.MapPost("/api/auth/set-manager-pin", (ManagerPinRequest dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Pin) || dto.Pin.Trim().Length < 4)
    {
        return Results.BadRequest(new { success = false, message = "PIN en az 4 haneli olmalıdır." });
    }
    Environment.SetEnvironmentVariable("CAFE_MANAGER_PIN", dto.Pin.Trim());
    return Results.Ok(new { success = true, pin = dto.Pin.Trim() });
});

// Garson Mobil Web Sayfasi Erisimi
app.MapGet("/garson", (IWebHostEnvironment env) =>
    Results.File(Path.Combine(env.WebRootPath, "garson", "index.html"), "text/html"));

app.MapFallbackToFile("/station/index.html");

app.Run();

static TicketDto ToTicket(Order order) => new(
    order.Id,
    order.Table.Number,
    order.Table.Name,
    order.CreatedAt,
    order.Note,
    order.Status,
    order.CreatedBy,
    order.Account?.OpenedBy ?? order.CreatedBy,
    order.Items.Where(i => !i.IsCancelled).Select(i => new TicketItemDto(
        i.ProductName,
        i.Quantity,
        i.UnitPrice,
        i.UnitPrice * i.Quantity)).ToList(),
    order.Items.Where(i => !i.IsCancelled).Sum(i => i.UnitPrice * i.Quantity));
