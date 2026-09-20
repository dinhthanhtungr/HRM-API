using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.ChangeManufacturingWorkInstructionStatus;

public sealed record ChangeManufacturingWorkInstructionStatusCommand(Guid TemplateId, ManufacturingTemplateLifecycleAction Action)
    : IRequest<OperationResult<ManufacturingWorkInstructionTemplateDto>>;
