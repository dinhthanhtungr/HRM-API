using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateBomFromSelectedFormula;

/// <summary>
/// Khởi tạo E-BOM Draft đầu tiên của Product từ Formula đã được khách hàng chọn/chốt.
/// </summary>
public sealed record CreateBomFromSelectedFormulaCommand(Guid ProductId)
    : IRequest<OperationResult<BomVersionDto>>;
