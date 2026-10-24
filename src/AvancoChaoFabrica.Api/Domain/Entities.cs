namespace AvancoChaoFabrica.Api.Domain;

public sealed class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? TaxId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? Code { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string? TaxId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum OrderKind
{
    ServiceOrder,
    ManufacturingOrder
}

public sealed class ProductionOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Number { get; set; }
    public OrderKind Kind { get; set; }
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string? Revision { get; set; }
    public DateOnly? DataDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ProductionItem> Items { get; set; } = [];
    public List<OrderStageWeight> StageWeights { get; set; } = [];
}

public sealed class ProductionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string? Drawing { get; set; }
    public string? Package { get; set; }
    public string? Tag { get; set; }
    public string? ReferenceTag { get; set; }
    public string? Mark { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitWeightKg { get; set; }
    public decimal? TotalWeightKg { get; set; }
    public DateOnly? ContractualDate { get; set; }
    public DateOnly? RescheduledDate { get; set; }
    public bool IsActive { get; set; } = true;
    public List<ItemStageProgress> StageProgress { get; set; } = [];
    public List<ShipmentLine> Shipments { get; set; } = [];
}

public sealed class OrderStageWeight
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public required string StageCode { get; set; }
    public required string StageName { get; set; }
    public int Sequence { get; set; }
    public decimal WeightPercent { get; set; }
}

public sealed class ItemStageProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductionItemId { get; set; }
    public ProductionItem? ProductionItem { get; set; }
    public required string StageCode { get; set; }
    public decimal CompletionPercent { get; set; }
    public decimal? CompletedWeightKg { get; set; }
    public DateOnly ReportDate { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ShipmentLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductionItemId { get; set; }
    public ProductionItem? ProductionItem { get; set; }
    public decimal Quantity { get; set; }
    public decimal? WeightKg { get; set; }
    public string? DocumentNumber { get; set; }
    public string? DocumentRevision { get; set; }
    public string? ManifestNumber { get; set; }
    public DateOnly? ShipmentDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LaborAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public string PersonOrTeam { get; set; } = string.Empty;
    public string? Role { get; set; }
    public DateOnly WorkDate { get; set; }
    public decimal Hours { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FileName { get; set; }
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ImportedBy { get; set; }
    public string Status { get; set; } = "Preview";
    public List<ImportedRow> Rows { get; set; } = [];
}

public sealed class ImportedRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public required string SheetName { get; set; }
    public int RowNumber { get; set; }
    public string? SourceDataDate { get; set; }
    public string? SourceRevision { get; set; }
    public string RawDataJson { get; set; } = "{}";
    public string Status { get; set; } = "Review";
    public string? ValidationMessage { get; set; }
}

public sealed class LocalUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string UserName { get; set; }
    public required string NormalizedUserName { get; set; }
    public required string PasswordHash { get; set; }
    public string Role { get; set; } = "Operator";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
