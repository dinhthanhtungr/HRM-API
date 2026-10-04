using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Queries.GetManufacturingProcessTemplateEditor;

public sealed record GetManufacturingProcessTemplateEditorQuery(Guid TemplateId)
    : IRequest<ManufacturingProcessTemplateEditorDto?>;
