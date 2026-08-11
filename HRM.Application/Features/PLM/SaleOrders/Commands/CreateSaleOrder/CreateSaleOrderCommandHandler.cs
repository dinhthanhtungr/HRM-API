using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrder.Services;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.SaleOrders.Services;
using MediatR;

namespace HRM.Application.Features.PLM.SaleOrders.Commands.CreateSaleOrder;

/// <summary>
/// Tạo SaleOrder New trong một transaction; không nhận trạng thái hoặc audit từ client.
/// </summary>
internal sealed class CreateSaleOrderCommandHandler
    : IRequestHandler<CreateSaleOrderCommand, OperationResult<CreateSaleOrderResultDto>>
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly SaleOrderCreationService _creationService;

    public CreateSaleOrderCommandHandler(
        ISaleOrderDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        SaleOrderCreationService creationService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _creationService = creationService;
    }

    public async Task<OperationResult<CreateSaleOrderResultDto>> Handle(
        CreateSaleOrderCommand command,
        CancellationToken cancellationToken)
    {
        var employeeId = SaleOrderCurrentUserGuard.RequireEmployeeId(_currentUser);
        var companyId = SaleOrderCurrentUserGuard.RequireCompanyId(_currentUser);
        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var createResult = await _creationService.CreateAsync(
            command.Request,
            employeeId,
            companyId,
            _dateTimeProvider.Now,
            cancellationToken);
        if (!createResult.Success || createResult.Data is null)
        {
            return OperationResult<CreateSaleOrderResultDto>.Fail(
                createResult.Message ?? "Không thể tạo SaleOrder.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return OperationResult<CreateSaleOrderResultDto>.Ok(
            SaleOrderCreationService.ToResultDto(createResult.Data),
            "Tạo đơn hàng thành công.");
    }
}
