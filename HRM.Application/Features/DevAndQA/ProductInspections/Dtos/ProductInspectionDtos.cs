namespace HRM.Application.Features.DevAndQA.ProductInspections.Dtos;

public sealed class ProductInspectionWriteRequest
{
    public Guid? ProductStandardId { get; set; }
    public string? BatchId { get; set; }
    public int? Weight { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Shape { get; set; }
    public bool? IsShapePass { get; set; }
    public string? ParticleSize { get; set; }
    public bool? IsParticleSizePass { get; set; }
    public string? PackingSpec { get; set; }
    public bool? IsPackingSpecPass { get; set; }
    public bool? VisualCheck { get; set; }
    public string? ColorDeltaE { get; set; }
    public bool? IsColorDeltaEPass { get; set; }
    public string? Moisture { get; set; }
    public bool? IsMoisturePass { get; set; }
    public string? Mfr { get; set; }
    public bool? IsMfrPass { get; set; }
    public string? FlexuralStrength { get; set; }
    public bool? IsFlexuralStrengthPass { get; set; }
    public string? Elongation { get; set; }
    public bool? IsElongationPass { get; set; }
    public string? Hardness { get; set; }
    public bool? IsHardnessPass { get; set; }
    public string? Density { get; set; }
    public bool? IsDensityPass { get; set; }
    public string? TensileStrength { get; set; }
    public bool? IsTensileStrengthPass { get; set; }
    public string? FlexuralModulus { get; set; }
    public bool? IsFlexuralModulusPass { get; set; }
    public string? ImpactResistance { get; set; }
    public bool? IsImpactResistancePass { get; set; }
    public string? Antistatic { get; set; }
    public bool? IsAntistaticPass { get; set; }
    public string? StorageCondition { get; set; }
    public bool? IsStorageConditionPass { get; set; }
    public string? IntrinsicViscosity { get; set; }
    public bool? IsIntrinsicViscosity { get; set; }
    public string? MeshType { get; set; }
    public bool? IsMeshAttached { get; set; }
    public bool? DwellTime { get; set; }
    public string? BlackDots { get; set; }
    public bool? MigrationTest { get; set; }
    public bool? DefectImpurity { get; set; }
    public bool? DefectBlackDot { get; set; }
    public bool? DefectShortFiber { get; set; }
    public bool? DefectMoist { get; set; }
    public bool? DefectDusty { get; set; }
    public bool? DefectWrongColor { get; set; }
    public string? Types { get; set; }
    public bool? DeliveryAccepted { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<string>? ClearFields { get; set; }
}

public class ProductInspectionDetailDto
{
    public Guid Id { get; set; }
    public string? ExternalId { get; set; }
    public Guid? ProductStandardId { get; set; }
    public string? BatchId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public int? Weight { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? ExpiryType { get; set; }
    public string? Shape { get; set; }
    public bool? IsShapePass { get; set; }
    public string? ParticleSize { get; set; }
    public bool? IsParticleSizePass { get; set; }
    public string? PackingSpec { get; set; }
    public bool? IsPackingSpecPass { get; set; }
    public bool? VisualCheck { get; set; }
    public string? ColorDeltaE { get; set; }
    public bool? IsColorDeltaEPass { get; set; }
    public string? Moisture { get; set; }
    public bool? IsMoisturePass { get; set; }
    public string? Mfr { get; set; }
    public bool? IsMfrPass { get; set; }
    public string? FlexuralStrength { get; set; }
    public bool? IsFlexuralStrengthPass { get; set; }
    public string? Elongation { get; set; }
    public bool? IsElongationPass { get; set; }
    public string? Hardness { get; set; }
    public bool? IsHardnessPass { get; set; }
    public string? Density { get; set; }
    public bool? IsDensityPass { get; set; }
    public string? TensileStrength { get; set; }
    public bool? IsTensileStrengthPass { get; set; }
    public string? FlexuralModulus { get; set; }
    public bool? IsFlexuralModulusPass { get; set; }
    public string? ImpactResistance { get; set; }
    public bool? IsImpactResistancePass { get; set; }
    public string? Antistatic { get; set; }
    public bool? IsAntistaticPass { get; set; }
    public string? StorageCondition { get; set; }
    public bool? IsStorageConditionPass { get; set; }
    public string? IntrinsicViscosity { get; set; }
    public bool? IsIntrinsicViscosity { get; set; }
    public string? MeshType { get; set; }
    public bool? IsMeshAttached { get; set; }
    public bool? DwellTime { get; set; }
    public string? BlackDots { get; set; }
    public bool? MigrationTest { get; set; }
    public bool? DefectImpurity { get; set; }
    public bool? DefectBlackDot { get; set; }
    public bool? DefectShortFiber { get; set; }
    public bool? DefectMoist { get; set; }
    public bool? DefectDusty { get; set; }
    public bool? DefectWrongColor { get; set; }
    public string? Types { get; set; }
    public bool? DeliveryAccepted { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreateDate { get; set; }
    public string? CreatedBy { get; set; }
}

public sealed class ProductInspectionSummaryDto
{
    public Guid Id { get; set; }
    public string? ExternalId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public string? BatchId { get; set; }
    public bool? Result { get; set; }
    public string? Types { get; set; }
    public DateTime? CreateDate { get; set; }
    public string? CreatedBy { get; set; }
}

public sealed class ProductCoaSourceDto
{
    public Guid MfgProductionOrderId { get; set; }
    public Guid? ProductStandardId { get; set; }
    public string? BatchId { get; set; }
    public IReadOnlyList<DateTime> LabelPrintDates { get; set; } = [];
    public string? ExpiryType { get; set; }
    public string? ProductPackage { get; set; }
    public decimal TotalQuantityRequest { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductExternalId { get; set; }
    public string? ProductName { get; set; }
    public string? ColourCode { get; set; }
}

public sealed class ProductInspectionWriteResultDto
{
    public Guid Id { get; set; }
    public string? ExternalId { get; set; }
}

public sealed class ProductInspectionPdfFileDto
{
    public string FileName { get; set; } = "COA.pdf";
    public byte[] Content { get; set; } = [];
}

public sealed class ProductInspectionPdfDocumentDto : ProductInspectionDetailDto
{
    public string? BagType { get; set; }
    public ProductInspectionPdfSpecificationsDto? Specifications { get; set; }
}

public sealed class ProductInspectionPdfSpecificationsDto
{
    public string? DeltaE { get; set; }
    public string? PelletSize { get; set; }
    public string? Moisture { get; set; }
    public string? Density { get; set; }
    public string? MeltIndex { get; set; }
    public string? TensileStrength { get; set; }
    public string? ElongationAtBreak { get; set; }
    public string? FlexuralStrength { get; set; }
    public string? FlexuralModulus { get; set; }
    public string? IzodImpactStrength { get; set; }
    public string? Hardness { get; set; }
    public string? Shape { get; set; }
    public string? DwellTime { get; set; }
    public string? BlackDots { get; set; }
    public string? MigrationTest { get; set; }
}
