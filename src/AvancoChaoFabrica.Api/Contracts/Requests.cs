using AvancoChaoFabrica.Api.Domain;

namespace AvancoChaoFabrica.Api.Contracts;

public sealed record CreateProjectRequest(string Name, string? Code, Guid CustomerId);
public sealed record UpdateProjectRequest(string Name, string? Code, Guid CustomerId, bool IsActive);
public sealed record CreateCustomerRequest(string Name, string? TaxId);
public sealed record UpdateCustomerRequest(string Name, string? TaxId, bool IsActive);
public sealed record CreateSupplierRequest(string Name, string? TaxId);
public sealed record UpdateSupplierRequest(string Name, string? TaxId, bool IsActive);
public sealed record CreateOrderRequest(
    string Number,
    OrderKind Kind,
    Guid ProjectId,
    Guid? SupplierId,
    string? Revision,
    DateOnly? DataDate);
public sealed record UpdateOrderRequest(
    string Number,
    OrderKind Kind,
    Guid ProjectId,
    Guid? SupplierId,
    string? Revision,
    DateOnly? DataDate,
    bool IsActive);
public sealed record CreateItemRequest(
    Guid ProductionOrderId,
    Guid? SupplierId,
    string? Drawing,
    string? Package,
    string? Tag,
    string? ReferenceTag,
    string? Mark,
    string? Description,
    decimal Quantity,
    string? Unit,
    decimal? UnitWeightKg,
    decimal? TotalWeightKg,
    DateOnly? ContractualDate,
    DateOnly? RescheduledDate);
public sealed record StageWeightRequest(
    string StageCode,
    string StageName,
    int Sequence,
    decimal WeightPercent);
public sealed record ReplaceStageWeightsRequest(IReadOnlyList<StageWeightRequest> Stages);
public sealed record RecordProgressRequest(
    string StageCode,
    decimal CompletionPercent,
    decimal? CompletedWeightKg,
    DateOnly ReportDate,
    string? Notes);
public sealed record CreateLaborAllocationRequest(
    string PersonOrTeam,
    string? Role,
    DateOnly WorkDate,
    decimal Hours,
    string? Notes);
public sealed record CreateShipmentLineRequest(
    decimal Quantity,
    decimal? WeightKg,
    string? DocumentNumber,
    string? DocumentRevision,
    string? ManifestNumber,
    DateOnly? ShipmentDate);
public sealed record ProjectResponse(Guid Id, string Name, string? Code, Guid CustomerId, bool IsActive);
public sealed record SupplierResponse(Guid Id, string Name, string? TaxId, bool IsActive);
public sealed record OrderResponse(
    Guid Id,
    string Number,
    OrderKind Kind,
    Guid ProjectId,
    Guid? SupplierId,
    string? Revision,
    DateOnly? DataDate,
    bool IsActive);
public sealed record ImportPreviewResponse(
    Guid Id,
    string FileName,
    DateTimeOffset ImportedAt,
    IReadOnlyList<ImportSheetPreview> Sheets,
    IReadOnlyList<SkippedSheetPreview> SkippedSheets);
public sealed record ImportSheetPreview(
    string SheetName,
    int HeaderRow,
    int DataStartRow,
    string TableType,
    string? SourceDataDate,
    string? SourceRevision,
    IReadOnlyList<string> Headers,
    IReadOnlyDictionary<string, string?> StageWeights,
    int TotalDataRows,
    bool IsTruncated,
    IReadOnlyList<ImportRowPreview> Rows);
public sealed record ImportRowPreview(
    int RowNumber,
    string Status,
    string? Message,
    IReadOnlyDictionary<string, string?> Values);
public sealed record SkippedSheetPreview(string SheetName, string Reason);
