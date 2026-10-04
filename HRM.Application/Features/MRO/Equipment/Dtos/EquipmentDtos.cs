using System.ComponentModel.DataAnnotations;

namespace HRM.Application.Features.MRO.Equipment.Dtos;

public sealed record EquipmentCapabilities(bool CanView, bool CanCreate, bool CanUpdate, bool CanDelete);
public sealed record EquipmentListItem(int EquipmentId, string EquipmentExternalId, string EquipmentName,
    string? GroupType, int AreaId, string AreaExternalId, Guid PartId, string? PartExternalId);
public sealed record EquipmentDetailDto(EquipmentListItem Machine, EquipmentDetailsRequest Details,
    IReadOnlyList<EquipmentSpecDto> Specifications);
public sealed record EquipmentSpecDto(int SpecId, string SpecKey, string? SpecValue, string? Unit,
    string? Note, DateTime? EnteredAt);
public sealed record EquipmentAreaOption(int AreaId, string AreaExternalId, string AreaName);
public sealed record EquipmentPartOption(Guid PartId, string ExternalId, string PartName);
public sealed record EquipmentTypeOption(int EquipmentTypeId, string EquipmentTypeName);
public sealed record EquipmentOptions(IReadOnlyList<EquipmentAreaOption> Areas,
    IReadOnlyList<EquipmentPartOption> Parts, IReadOnlyList<EquipmentTypeOption> Types,
    IReadOnlyList<string> GroupTypes);

public sealed class EquipmentListRequest
{
    [StringLength(200)] public string? Keyword { get; set; }
    [StringLength(100)] public string? GroupType { get; set; }
    [Range(1, int.MaxValue)] public int? AreaId { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public sealed class SaveEquipmentRequest
{
    [Required, StringLength(100)] public string EquipmentExternalId { get; set; } = "";
    [Required, StringLength(200)] public string EquipmentName { get; set; } = "";
    [StringLength(100)] public string? GroupType { get; set; }
    [Range(1, int.MaxValue)] public int AreaId { get; set; }
    public Guid PartId { get; set; }
    [Required] public EquipmentDetailsRequest Details { get; set; } = new();
}

public sealed class EquipmentDetailsRequest
{
    [StringLength(100)] public string? SerialNo { get; set; }
    [StringLength(200)] public string? Manufacturer { get; set; }
    [StringLength(100)] public string? Model { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? CommissioningDate { get; set; }
    public DateTime? WarrantyUntil { get; set; }
    [StringLength(4000)] public string? Notes { get; set; }
    [Range(1, int.MaxValue)] public int? EquipmentTypeId { get; set; }
}

public sealed class SaveEquipmentSpecRequest
{
    [Required, StringLength(100)] public string SpecKey { get; set; } = "";
    [StringLength(1000)] public string? SpecValue { get; set; }
    [StringLength(50)] public string? Unit { get; set; }
    [StringLength(2000)] public string? Note { get; set; }
}
