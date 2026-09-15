using HRM.Application.Features.PLM.PrintLabels.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.PrintLabels.Queries.GetPrintLabelTemplateSelection;

public sealed record GetPrintLabelTemplateSelectionQuery(Guid PrintLabelTemplateId) : IRequest<PrintLabelTemplateSelectionDto?>;
