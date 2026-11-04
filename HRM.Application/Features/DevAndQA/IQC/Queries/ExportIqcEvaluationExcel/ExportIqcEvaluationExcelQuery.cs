using MediatR;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
namespace HRM.Application.Features.DevAndQA.IQC.Queries.ExportIqcEvaluationExcel;
/// <summary>Xuất phiếu hiện tại và một trang lịch sử cùng mặt hàng; yêu cầu ViewIqc/company.</summary>
public sealed record ExportIqcEvaluationExcelQuery(long VoucherDetailId, int PageNumber = 1, int PageSize = 3)
    : IRequest<IqcFileDto>;
