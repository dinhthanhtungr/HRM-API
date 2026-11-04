using HRM.Application.Abstractions.Security;
using HRM.Application.Features.DevAndQA.IQC.Dtos;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Enums.Devandqa;
using HRM.Domain.Enums.WareHouses;

namespace HRM.Application.Features.DevAndQA.IQC.Services;

/// <summary>Quy tắc thuần của IQC: validation, mapping quyết định sang loại phiếu, trạng thái PO và DTO mutation.</summary>
internal static class IqcRules
{
    public static Guid? Company(ICurrentUser user) =>
        user.IsAuthenticated && user.CompanyId is { } id && id != Guid.Empty ? id : null;

    public static string? Validate(QCInputQuery query)
    {
        if (query.VoucherDetailId < 0 || query.QCInputByQCId == Guid.Empty)
            return "Id không hợp lệ.";
        if (query.FromDate > query.ToDate)
            return "FromDate không được sau ToDate.";
        if (query.VoucherDetailType.HasValue && !Enum.IsDefined(query.VoucherDetailType.Value))
            return "VoucherDetailType không hợp lệ.";
        var page = query.PageNumber <= 0 ? 1 : query.PageNumber;
        var size = query.PageSize <= 0 ? 20 : query.PageSize;
        if ((long)(page - 1) * size > int.MaxValue)
            return "Khoảng phân trang không hợp lệ.";
        return null;
    }

    public static string? ValidateDecision(QcDecision? decision) =>
        decision.HasValue && !Enum.IsDefined(decision.Value) ? "ImportWarehouseType không hợp lệ." : null;

    public static VoucherDetailType ToVoucherType(QcDecision decision) => decision switch
    {
        QcDecision.QCPass => VoucherDetailType.QCPass,
        QcDecision.QCFail => VoucherDetailType.QCFail,
        QcDecision.Special => VoucherDetailType.Special,
        QcDecision.Waiter => VoucherDetailType.Waiter,
        _ => throw new ArgumentOutOfRangeException(nameof(decision))
    };

    public static bool IsAccepted(QcDecision? decision) => decision is QcDecision.QCPass or QcDecision.Special;

    // PO status strings are the existing persisted contract.
    public static string? PurchaseOrderStatus(IEnumerable<PurchaseOrderDetail> details)
    {
        var active = details.Where(x => x.IsActive).ToArray();
        if (active.Length == 0) return null;
        if (active.All(x => (x.RealQuantity ?? 0m) >= (x.RequestQuantity ?? 0m))) return "Completed";
        return active.Any(x => (x.RealQuantity ?? 0m) > 0m) ? "InProgress" : "Pending";
    }

    public static string ToVietnamese(VoucherDetailType type) => type switch
    {
        VoucherDetailType.Special => "Chấp nhận đặc biệt",
        VoucherDetailType.QCPass => "Chấp nhận",
        VoucherDetailType.Waiter => "Tạm giữ",
        VoucherDetailType.QCFail => "Từ chối",
        _ => "Không xác định"
    };

    // POST and PATCH intentionally return the legacy mutation DTO, not the detail context.
    public static GetQCInputByQC ToMutationDto(QCInputByQC entity, bool includeCreatedDate = false) => new()
    {
        QCInputByQCId = entity.QCInputByQCId,
        VoucherDetailId = entity.VoucherDetailId,
        MaterialName = entity.MaterialName,
        MaterialExternalId = entity.MaterialExternalId,
        InspectionMethod = entity.InspectionMethod,
        IsCOAProvided = entity.IsCOAProvided,
        IsMSDSTDSProvided = entity.IsMSDSTDSProvided,
        IsMetalDetectionRequired = entity.IsMetalDetectionRequired,
        IsSuccessQuality = entity.IsSuccessQuality,
        ImportWarehouseType = entity.ImportWarehouseType,
        Note = entity.Note,
        AttachmentCollectionId = entity.AttachmentCollectionId,
        AttachmentStatus = entity.AttachmentStatus,
        AttachmentLastError = entity.AttachmentLastError,
        QCCreatedDate = includeCreatedDate ? entity.CreatedDate : null
    };
}
