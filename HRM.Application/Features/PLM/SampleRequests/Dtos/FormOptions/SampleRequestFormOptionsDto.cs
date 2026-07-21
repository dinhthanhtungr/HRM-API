namespace HRM.Application.Features.PLM.SampleRequests.Dtos.FormOptions;

public sealed class SampleRequestFormOptionsDto
{
    public IReadOnlyList<SampleRequestColorOptionDto> Colors { get; set; } = Array.Empty<SampleRequestColorOptionDto>();
    public IReadOnlyList<SampleRequestAdditiveOptionDto> Additives { get; set; } = Array.Empty<SampleRequestAdditiveOptionDto>();
    public IReadOnlyList<SampleRequestOptionDto> Categories { get; set; } = Array.Empty<SampleRequestOptionDto>();
    public IReadOnlyList<SampleRequestOptionDto> Branches { get; set; } = Array.Empty<SampleRequestOptionDto>();
}
