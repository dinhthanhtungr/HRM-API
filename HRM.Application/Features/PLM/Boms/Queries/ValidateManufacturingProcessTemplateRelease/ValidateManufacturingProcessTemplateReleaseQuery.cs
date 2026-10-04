using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.ValidateManufacturingProcessTemplateRelease;

public sealed record ValidateManufacturingProcessTemplateReleaseQuery(Guid TemplateId)
    : IRequest<ManufacturingProcessTemplateValidationDto?>;
