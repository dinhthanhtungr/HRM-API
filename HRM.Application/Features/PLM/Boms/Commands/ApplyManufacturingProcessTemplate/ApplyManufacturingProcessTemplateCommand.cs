using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.ApplyManufacturingProcessTemplate;

public sealed record ApplyManufacturingProcessTemplateCommand(Guid BomVersionId, Guid ProcessTemplateId, bool IsPreview)
    : IRequest<OperationResult<ManufacturingProcessTemplateApplicationDto>>;
