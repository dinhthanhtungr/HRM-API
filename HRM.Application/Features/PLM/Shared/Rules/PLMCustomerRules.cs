using HRM.Domain.Entities.CustomerSchema;
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
}
