using HRM.Application.Commons.Models;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.Manufacturing.Dtos.ColorChipManufacturingRecords.GetDtos;
using HRM.Domain.Enums.SampleRequests;
using MediatR;

namespace HRM.Application.Features.PLM.Manufacturing.Queries.ListManufacturingColorChips;

public sealed record ListManufacturingColorChipsQuery : IRequest<OperationResult<PagedResult<GetColorChipManufacturingRecordSummary>>>, HRM.Application.Commons.Searching.ISmartSearchRequest
{
    public string? SearchSelections { get; init; }
    public string? SuggestionKeyword { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 15;
    public Guid? Id { get; init; }
    public Guid? OrderId { get; init; }
    public Guid? FormulaId { get; init; }
    public ResinType? ResinType { get; init; }
    public LogoType? LogoType { get; init; }
    public FormStyle? FormStyle { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Keyword { get; init; }
}
