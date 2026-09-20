using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.UpsertManufacturingWorkInstruction;

public sealed record UpsertManufacturingWorkInstructionCommand(Guid? TemplateId, UpsertManufacturingWorkInstructionTemplateRequest Request)
    : IRequest<OperationResult<ManufacturingWorkInstructionTemplateDto>>;
