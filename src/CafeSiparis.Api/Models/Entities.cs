namespace CafeSiparis.Api.Models;

public class DiningTable
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public string Section { get; set; } = "Genel"; // "Sol", "Sağ", "Dış", "İç", "Kapı"
    public List<TableAccount> Accounts { get; set; } = [];
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public List<Product> Products { get; set; } = [];
}

public class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; } = false;
}

public class TableAccount
{
    public int Id { get; set; }
    public int TableId { get; set; }
    public DiningTable Table { get; set; } = null!;
    public DateTime OpenedAt { get; set; } = DateTime.Now;
    public DateTime? ClosedAt { get; set; }
    public bool IsClosed { get; set; } = false;
    public decimal PaidAmount { get; set; } = 0;
    public string OpenedBy { get; set; } = "Garson";
    public List<Order> Orders { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}

public class Order
{
    public int Id { get; set; }
    public int? AccountId { get; set; }
    public TableAccount? Account { get; set; }
    public int TableId { get; set; }
    public DiningTable Table { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Status { get; set; } = "Yeni"; // "Yeni", "Yazdirildi", "Iptal"
    public string? Note { get; set; }
    public string CreatedBy { get; set; } = "Garson"; // "Garson", "Kasa"
    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int PaidQuantity { get; set; } = 0;
    public bool IsCancelled { get; set; } = false;
    public string? CancelReason { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public TableAccount Account { get; set; } = null!;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Nakit"; // "Nakit", "Kredi Karti"
    public DateTime PaidAt { get; set; } = DateTime.Now;
    public string? Note { get; set; }
}
