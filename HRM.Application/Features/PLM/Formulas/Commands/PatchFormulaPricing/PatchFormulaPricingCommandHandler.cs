using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Commands.PatchFormulaPricing;

/// <summary>
/// Compatibility endpoint retained for old clients. Formula pricing fields are read-only;
/// all new pricing writes must create or update a ProductPricingVersion.
/// </summary>
[Obsolete("Formula pricing is read-only. Use ProductPricingVersion pricing APIs.")]
internal sealed class PatchFormulaPricingCommandHandler
    : IRequestHandler<PatchFormulaPricingCommand, OperationResult<FormulaPricingResultDto>>
{
    public Task<OperationResult<FormulaPricingResultDto>> Handle(
        PatchFormulaPricingCommand command,
        CancellationToken cancellationToken)
        => Task.FromResult(OperationResult<FormulaPricingResultDto>.Fail(
            "PatchFormulaPricingDeprecated: Formula pricing is read-only. " +
            "Create or update a ProductPricingVersion instead."));
}
