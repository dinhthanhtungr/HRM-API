using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.Quotations.Commands.DeleteQuotation;

internal sealed class DeleteQuotationCommandHandler(
    ICRMReadDbContext readDbContext,
    ICRMWriteDbContext writeDbContext,
    ICustomerVisibilityService visibilityService,
    KeyedMutationLock<Guid> mutationLock,
    QuotationAtomicSaveService atomicSaveService,
    IDateTimeProvider clock)
    : IRequestHandler<DeleteQuotationCommand, OperationResult<QuotationTotalsDto>>
{
    public async Task<OperationResult<QuotationTotalsDto>> Handle(
        DeleteQuotationCommand command, CancellationToken cancellationToken)
    {
        if (command.QuotationId == Guid.Empty || !command.ExpectedUpdatedDate.HasValue)
            return OperationResult<QuotationTotalsDto>.Fail("QuotationId and ExpectedUpdatedDate are required.");

        using var lease = await mutationLock.AcquireAsync(command.QuotationId, cancellationToken);
        return await atomicSaveService.ExecuteAsync(async () =>
        {
            var scope = await visibilityService.BuildScopeAsync(cancellationToken);
            var quotation = await writeDbContext.Quotations.AsTracking().FirstOrDefaultAsync(x =>
                x.QuotationId == command.QuotationId && x.CompanyId == scope.CompanyId && x.IsActive,
                cancellationToken);
            if (quotation is null || !await visibilityService
                    .ApplyCustomerVisibility(readDbContext.Customers.AsNoTracking(), scope)
                    .AnyAsync(x => x.CustomerId == quotation.CustomerId, cancellationToken))
                return OperationResult<QuotationTotalsDto>.Fail(
                    "Quotation was not found or is outside your visibility scope.");

            var conflict = OptimisticConcurrencyHelper.ValidateExpectedUpdatedDate(
                command.ExpectedUpdatedDate, quotation.UpdatedDate ?? quotation.CreatedDate, "Quotation");
            if (conflict is not null)
                return OperationResult<QuotationTotalsDto>.Fail(conflict);
            // Thu hồi qua workflow hiện hành trước khi xóa; không bỏ qua yêu cầu đang chờ hoặc lịch sử đã gửi.
            if (quotation.Status != QuotationStatus.Draft || quotation.SentDate.HasValue)
                return OperationResult<QuotationTotalsDto>.Fail(
                    "Only an unsent Draft quotation can be deleted. Withdraw pending requests first.");

            quotation.IsActive = false;
            quotation.UpdatedBy = scope.EmployeeId;
            quotation.UpdatedDate = QuotationWorkflowRules.NextUpdatedDate(clock.Now, quotation.UpdatedDate);
            return OperationResult<QuotationTotalsDto>.Ok(new QuotationTotalsDto
            {
                Status = quotation.Status, UpdatedDate = quotation.UpdatedDate
            }, "Quotation deleted successfully.");
        }, cancellationToken);
    }
}
