using HRM.Application.Features.PLM.PrintLabels.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.PrintLabels.Queries.GetTemplateSelection;

public sealed record GetTemplateSelectionQuery(Guid PrintLabelTemplateId) : IRequest<PrintLabelTemplateSelectionDto?>;
