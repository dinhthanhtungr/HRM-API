using HRM.Application.Commons.Models;
using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using MediatR;

namespace HRM.Application.Features.DevAndQA.ProductInspections.Queries.ExportProductInspectionPdf;

public sealed record ExportProductInspectionPdfQuery(Guid Id, bool TemplateOnly)
    : IRequest<OperationResult<ProductInspectionPdfFileDto>>;
