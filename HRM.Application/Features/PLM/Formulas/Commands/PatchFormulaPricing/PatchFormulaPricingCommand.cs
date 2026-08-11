using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Commands.PatchFormulaPricing;

public sealed record PatchFormulaPricingCommand(
    Guid FormulaId,
    PatchFormulaPricingRequest Request)
    : IRequest<OperationResult<FormulaPricingResultDto>>;
