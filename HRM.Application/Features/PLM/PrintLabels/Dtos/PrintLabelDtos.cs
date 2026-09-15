namespace HRM.Application.Features.PLM.PrintLabels.Dtos;

public sealed class PrintLabelElementRequest
{
    public int LineNo { get; init; }
    public string FieldKey { get; init; } = string.Empty;
    public string? DefaultValue { get; init; }
}

public sealed class PrintLabelTemplateSelectionDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal WidthMm { get; init; }
    public decimal HeightMm { get; init; }
    public Guid? AttachmentCollectionId { get; init; }
    public IReadOnlyList<PrintLabelLogoDto> Logos { get; init; } = Array.Empty<PrintLabelLogoDto>();
    public IReadOnlyList<PrintLabelElementDto> Elements { get; init; } = Array.Empty<PrintLabelElementDto>();
}

public sealed class PrintLabelLogoDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public Guid AttachmentCollectionId { get; init; }
    public bool IsDefault { get; init; }
}

public sealed class PrintLabelElementDto
{
    public Guid Id { get; init; }
    public int LineNo { get; init; }
    public string FieldKey { get; init; } = string.Empty;
    public string? DefaultValue { get; init; }
}
