namespace CafeSiparis.Api;

public record TableDto(int Id, int Number, string Name, string Section);
public record ProductDto(int Id, string Name, decimal Price, bool IsFeatured = false);
public record CategoryDto(int Id, string Name, List<ProductDto> Products);

public record CreateOrderItemDto(int ProductId, int Quantity);
public record CreateOrderDto(int TableId, string? Note, List<CreateOrderItemDto> Items, string? CreatedBy = "Garson");

public record TicketItemDto(string Name, int Quantity, decimal UnitPrice, decimal LineTotal);
public record TicketDto(
    int OrderId,
    int TableNumber,
    string TableName,
    DateTime CreatedAt,
    string? Note,
    string Status,
    string CreatedBy,
    string OpenedBy,
    List<TicketItemDto> Items,
    decimal Total);

public record TableStatusDto(
    int Id,
    int Number,
    string Name,
    string Section,
    bool IsOccupied,
    int? ActiveAccountId,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    DateTime? OpenedAt,
    int ItemCount);

public record OrderItemDetailDto(
    int Id,
    int OrderId,
    int ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    int PaidQuantity,
    int RemainingQuantity,
    decimal LineTotal,
    DateTime CreatedAt,
    string CreatedBy,
    bool IsCancelled);

public record PaymentDto(
    int Id,
    decimal Amount,
    string PaymentMethod,
    DateTime PaidAt,
    string? Note);

public record TableAccountDetailDto(
    int AccountId,
    int TableId,
    int TableNumber,
    string TableName,
    string Section,
    DateTime OpenedAt,
    string OpenedBy,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    List<OrderItemDetailDto> Items,
    List<PaymentDto> Payments);

public record TransferTableRequest(int TargetTableId);
public record MergeTableRequest(int TargetTableId);

public record PayItemRequest(int OrderItemId, int Quantity);
public record PayAccountRequest(
    decimal Amount,
    string PaymentMethod,
    bool CloseTableIfFullyPaid,
    List<PayItemRequest>? ItemsToPay = null,
    string? Note = null);

public record ManagerPinRequest(string Pin);
