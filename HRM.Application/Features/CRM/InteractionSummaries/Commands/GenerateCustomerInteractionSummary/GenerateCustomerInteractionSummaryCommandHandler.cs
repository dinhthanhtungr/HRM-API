using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Services;
using HRM.Application.Features.CRM.InteractionSummaries.Dtos;
using HRM.Application.Features.CRM.InteractionSummaries.Services.GenerateCustomerInteractionSummary;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.InteractionSummaries.Commands.GenerateCustomerInteractionSummary;

/// <summary>
/// Xác thực company/customer/employee scope trước khi chuyển sang generation service dùng chung với worker.
/// </summary>
internal sealed class GenerateCustomerInteractionSummaryCommandHandler
    : IRequestHandler<GenerateCustomerInteractionSummaryCommand, OperationResult<CustomerInteractionAiSummaryDto>>
{
    private readonly CustomerCrmAccessService _accessService;
    private readonly CustomerInteractionAiSummaryGenerationService _generationService;

    public GenerateCustomerInteractionSummaryCommandHandler(
        CustomerCrmAccessService accessService,
        CustomerInteractionAiSummaryGenerationService generationService)
    {
        _accessService = accessService;
        _generationService = generationService;
    }

    public async Task<OperationResult<CustomerInteractionAiSummaryDto>> Handle(
        GenerateCustomerInteractionSummaryCommand command,
        CancellationToken cancellationToken)
    {
        if (command.CustomerId == Guid.Empty)
        {
            return OperationResult<CustomerInteractionAiSummaryDto>.Fail("Customer id is required.");
        }

        if (!Enum.IsDefined(command.Request.SummaryScope))
        {
            return OperationResult<CustomerInteractionAiSummaryDto>.Fail("Summary scope is invalid.");
        }

        var scope = await _accessService.BuildScopeAsync(cancellationToken);
        var customerExists = await _accessService.VisibleCustomers(scope)
            .AnyAsync(x => x.CustomerId == command.CustomerId, cancellationToken);
        if (!customerExists)
        {
            return OperationResult<CustomerInteractionAiSummaryDto>.Fail("Customer was not found.");
        }

        var employee = await _accessService.ResolveEmployeeAsync(
            scope,
            command.Request.SaleEmployeeId,
            false,
            cancellationToken);
        if (!employee.IsAllowed)
        {
            return OperationResult<CustomerInteractionAiSummaryDto>.Fail(
                "Sale employee is outside your scope.");
        }

        var outcome = await _generationService.GenerateAsync(
            command.CustomerId,
            scope.CompanyId,
            employee.EmployeeId,
            scope.EmployeeId,
            command.Request,
            cancellationToken);
        return outcome.Result;
    }
}
