using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingBomFromSelectedFormula;

/// <summary>
/// Khởi tạo idempotent M-BOM Draft từ Formula Completed đang được chọn của Product.
/// Đây là luồng độc lập, không thay đổi contract E-BOM hoặc M-BOM hiện hành.
/// </summary>
public sealed record CreateManufacturingBomFromSelectedFormulaCommand(Guid ProductId)
    : IRequest<OperationResult<FormulaDrivenManufacturingBomDto>>;
