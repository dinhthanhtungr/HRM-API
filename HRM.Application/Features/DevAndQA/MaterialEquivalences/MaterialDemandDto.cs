namespace HRM.Application.Features.DevAndQA.MaterialEquivalences;

public sealed record MaterialDemandOrderDto(Guid Id, string Code, string Status, DateTime RequiredDate, decimal RemainingKg, bool IsInternal);
public sealed record MaterialDemandDto(string Code, string Name, string? ProcessingType,
    decimal RemainingKg, decimal OnHandKg, decimal AvailableKg, decimal ShortageKg, decimal? DefectiveOnHandKg,
    EquivalentMaterialDto? SourceMaterial, IReadOnlyList<MaterialDemandOrderDto> Orders);
