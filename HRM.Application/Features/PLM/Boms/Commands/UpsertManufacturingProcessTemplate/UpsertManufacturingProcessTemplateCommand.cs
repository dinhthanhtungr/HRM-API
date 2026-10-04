using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.UpsertManufacturingProcessTemplate;

public sealed record UpsertManufacturingProcessTemplateCommand(Guid? TemplateId, UpsertManufacturingProcessTemplateRequest Request)
    : IRequest<OperationResult<ManufacturingProcessTemplateEditorDto>>;
