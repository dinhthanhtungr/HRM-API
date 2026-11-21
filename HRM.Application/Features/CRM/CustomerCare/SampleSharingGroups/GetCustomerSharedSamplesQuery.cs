using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.SampleSharingGroups;

public sealed record SharedCustomerSampleDto(Guid SampleRequestId, string Code, Guid ProductId, string? ProductCode,
    string? ProductName, Guid OwnerCustomerId, string? OwnerCode, string? OwnerName, bool CanOrder, bool CanQuote,
    string? OrderDisabledReason, string? QuoteDisabledReason, string TargetCode, string TargetName);
public sealed record GetCustomerSharedSamplesQuery(Guid CustomerId, string? Keyword) : IRequest<OperationResult<IReadOnlyList<SharedCustomerSampleDto>>>;
