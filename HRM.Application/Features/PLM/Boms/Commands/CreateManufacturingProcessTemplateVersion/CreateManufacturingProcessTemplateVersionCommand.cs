using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingProcessTemplateVersion;

public sealed record CreateManufacturingProcessTemplateVersionCommand(Guid SourceTemplateId, string? ChangeReason) : IRequest<OperationResult<ManufacturingProcessTemplateDto>>;
