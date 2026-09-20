using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Domain.Enums.Boms;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplates;

public sealed record GetManufacturingProcessTemplatesQuery(Guid? TemplateId, ManufacturingTemplateStatus? Status) : IRequest<IReadOnlyList<ManufacturingProcessTemplateDto>>;
