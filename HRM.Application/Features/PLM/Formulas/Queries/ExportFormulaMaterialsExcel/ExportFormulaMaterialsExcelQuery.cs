using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Formulas.Dtos.Exports;
using MediatR;

namespace HRM.Application.Features.PLM.Formulas.Queries.ExportFormulaMaterialsExcel;

/// <summary>Xuất danh sách NVL của Formula cùng đơn giá mới nhất thành tệp Excel.</summary>
public sealed record ExportFormulaMaterialsExcelQuery(Guid FormulaId)
    : IRequest<OperationResult<FormulaMaterialsExcelExportFileDto>>;
