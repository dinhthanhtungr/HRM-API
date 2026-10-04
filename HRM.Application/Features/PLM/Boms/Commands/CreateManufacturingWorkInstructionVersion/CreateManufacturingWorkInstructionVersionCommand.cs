using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingWorkInstructionVersion;

public sealed record CreateManufacturingWorkInstructionVersionCommand(
    Guid SourceTemplateId,
    string? ChangeReason)
    : IRequest<OperationResult<ManufacturingWorkInstructionTemplateDto>>;
