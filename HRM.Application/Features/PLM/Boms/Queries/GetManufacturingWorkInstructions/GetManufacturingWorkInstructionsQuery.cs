using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingWorkInstructions;

public sealed record GetManufacturingWorkInstructionsQuery(Guid? TemplateId, ManufacturingTemplateStatus? Status)
    : IRequest<IReadOnlyList<ManufacturingWorkInstructionTemplateDto>>;
