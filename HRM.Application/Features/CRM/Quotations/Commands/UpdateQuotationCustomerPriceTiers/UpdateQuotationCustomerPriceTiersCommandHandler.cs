using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotationCustomerPriceTiers;

internal sealed class UpdateQuotationCustomerPriceTiersCommandHandler(
    QuotationCustomerPriceUpdateService service,
    KeyedMutationLock<Guid> mutationLock)
    : IRequestHandler<UpdateQuotationCustomerPriceTiersCommand, OperationResult<QuotationTotalsDto>>
{
    public async Task<OperationResult<QuotationTotalsDto>> Handle(
        UpdateQuotationCustomerPriceTiersCommand command, CancellationToken cancellationToken)
    {
        using var lease = await mutationLock.AcquireAsync(command.QuotationId, cancellationToken);
        return await service.ApplyAsync(
            command.QuotationId, command.Request, command.Request.ExpectedUpdatedDate,
            persist: true, cancellationToken);
    }
}