using HRM.Application.Commons.Pagination;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.DevAndQA.IQC.Dtos;

public class QCInputQuery
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public Guid? QCInputByQCId { get; set; }
    public long VoucherDetailId { get; set; }
    public string? Keyword { get; set; }
    public bool? HasQC { get; set; }
    public VoucherDetailType? VoucherDetailType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
