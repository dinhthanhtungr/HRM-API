using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.ChangeManufacturingProcessTemplateStatus;

public sealed record ChangeManufacturingProcessTemplateStatusCommand(Guid TemplateId, ManufacturingTemplateLifecycleAction Action)
    : IRequest<OperationResult<ManufacturingProcessTemplateDto>>;
