using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Services;
using MediatR;

namespace HRM.Application.Features.CRM.CustomerCare.Commands.DeactivateCustomer;

internal sealed class DeactivateCustomerCommandHandler
    : IRequestHandler<DeactivateCustomerCommand, OperationResult<CustomerActivationResultDto>>
{
    private readonly CustomerActivationService _activationService;

    public DeactivateCustomerCommandHandler(CustomerActivationService activationService)
    {
        _activationService = activationService;
    }

    public Task<OperationResult<CustomerActivationResultDto>> Handle(
        DeactivateCustomerCommand request,
        CancellationToken cancellationToken)
        => _activationService.SetActiveAsync(request.CustomerId, isActive: false, cancellationToken);
}
