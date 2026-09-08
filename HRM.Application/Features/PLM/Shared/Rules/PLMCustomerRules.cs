using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.Merchadises;
using HRM.Application.Commons.Rules;

namespace HRM.Application.Features.PLM.Shared.Rules;

/// <summary>
/// Quy tắc nhận diện khách nội bộ dùng chung trong PLM để dashboard, sale order,
/// báo cáo và các flow lifecycle không tự hard-code nhiều kiểu khác nhau.
/// </summary>
internal static class PLMCustomerRules
{
    public const string InternalCustomerExternalId = InternalCustomerRules.InternalCustomerExternalId;

    public static bool IsInternalCustomer(Customer customer)
    {
        return IsInternalCustomerExternalId(customer.ExternalId);
    }

    public static bool IsInternalCustomerExternalId(string? externalId)
    {
        return InternalCustomerRules.IsInternalCustomerExternalId(externalId);
    }

    /// <summary>
    /// Cả bốn loại đơn đều nhận Formula gắn Sample Request đã Gửi mẫu hoặc Hoàn thành.
    /// Client cũ không gửi OrderType vẫn giữ lifecycle cũ: chỉ KH_VIETAUS nhận SampleSent.
    /// </summary>
    public static bool AllowsSampleSentFormula(
        OrderType? orderType,
        bool isInternalCustomer)
    {
        return orderType is { } value && Enum.IsDefined(value) || isInternalCustomer;
    }

    /// <summary>
    /// Chỉ đơn Nội bộ của KH_VIETAUS được chọn Formula/Sample Request từ mọi khách hàng.
    /// Các loại đơn khác vẫn chỉ dùng dữ liệu của khách đang lập đơn hoặc KH_VIETAUS.
    /// </summary>
    public static bool CanUseAllCustomersFormula(
        OrderType? orderType,
        bool isInternalCustomer)
    {
        return orderType == OrderType.Internal && isInternalCustomer;
    }
}
