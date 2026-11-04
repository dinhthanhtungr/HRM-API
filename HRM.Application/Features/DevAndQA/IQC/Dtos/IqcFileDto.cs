namespace HRM.Application.Features.DevAndQA.IQC.Dtos;

public sealed class IqcFileDto
{
    public required byte[] Content { get; init; }
    public required string FileName { get; init; }
    public string ContentType { get; init; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
