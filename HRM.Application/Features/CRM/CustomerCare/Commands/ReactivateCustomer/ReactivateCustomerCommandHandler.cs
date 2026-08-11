using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.ReactivateCustomer;

internal sealed class ReactivateCustomerCommandHandler
    : IRequestHandler<ReactivateCustomerCommand, OperationResult<CustomerActivationResultDto>>
{
    private readonly CustomerActivationService _activationService;

    public ReactivateCustomerCommandHandler(CustomerActivationService activationService)
    {
        _activationService = activationService;
    }

    public Task<OperationResult<CustomerActivationResultDto>> Handle(
        ReactivateCustomerCommand request,
        CancellationToken cancellationToken)
        => _activationService.SetActiveAsync(request.CustomerId, isActive: true, cancellationToken);
}
