using System.ComponentModel.DataAnnotations;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Reports;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using MediatR;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailySuggestions;

public sealed class GetSampleRequestDailySuggestionsQuery : IRequest<SampleRequestDailySuggestionsDto>
{
    [Required]
    [StringLength(80, MinimumLength = 2)]
    public string Q { get; init; } = string.Empty;
    public DateOnly? Date { get; init; }
    public SampleRequestDailyView View { get; init; } = SampleRequestDailyView.Today;
    public Guid? CustomerId { get; init; }
}
