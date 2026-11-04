using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using MediatR;

namespace HRM.Application.Features.CRM.Quotations.Commands.UpdateQuotationCustomerPriceTiers;

internal sealed class UpdateQuotationCustomerPriceTiersCommandHandler(
    QuotationCustomerPriceUpdateService service,
    KeyedMutationLock<Guid> mutationLock,
    QuotationAtomicSaveService atomicSaveService,
    QuotationSendService sendService)
    : IRequestHandler<UpdateQuotationCustomerPriceTiersCommand, OperationResult<QuotationTotalsDto>>
{
    public async Task<OperationResult<QuotationTotalsDto>> Handle(
        UpdateQuotationCustomerPriceTiersCommand command, CancellationToken cancellationToken)
    {
        using var lease = await mutationLock.AcquireAsync(command.QuotationId, cancellationToken);
        return await atomicSaveService.ExecuteAsync(async () =>
        {
            var result = await service.ApplyAsync(command.QuotationId, command.Request,
                command.Request.ExpectedUpdatedDate, persist: false, cancellationToken);
            if (!result.Success || result.Data?.Status != HRM.Domain.Enums.CustomerEnum.QuotationStatus.PendingSendConfirmation)
                return result;
            if (result.Data.UpdatedDate == command.Request.ExpectedUpdatedDate)
                return result;
            return await sendService.ReevaluatePendingAsync(command.QuotationId, command.Request.SendConfirmationNote, command.Request.ExpectedUpdatedDate, cancellationToken);
        }, cancellationToken);
    }
}
