using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using MediatR;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateBom;

/// <summary>Tạo E-BOM cùng phiên bản Draft đầu tiên và toàn bộ dòng vật tư.</summary>
public sealed record CreateBomCommand(CreateBomRequest Request)
    : IRequest<OperationResult<BomVersionDto>>;
