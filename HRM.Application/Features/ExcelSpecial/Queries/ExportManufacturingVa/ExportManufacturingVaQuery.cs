using HRM.Application.Commons.Models;
using MediatR;

namespace HRM.Application.Features.ExcelSpecial.Queries.ExportManufacturingVa;

/// <summary>Xuất VA và định mức theo mẫu AMIS, trong công ty đăng nhập; ngày lọc là ngày tạo VA.</summary>
public sealed record ExportManufacturingVaQuery : IRequest<OperationResult<byte[]>>
{
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public Guid[]? ManufacturingFormulaIds { get; init; }
}
