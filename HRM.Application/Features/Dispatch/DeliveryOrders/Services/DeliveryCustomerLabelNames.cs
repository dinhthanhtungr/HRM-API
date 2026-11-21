using HRM.Domain.Entities.PrintectSchema;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Services;

/// <summary>Tên trên nhãn khách hàng, chỉ đọc FieldKey=Name active và cả hai parent cùng company.</summary>
internal static class DeliveryCustomerLabelNames
{
    public static IQueryable<CustomerLabelDetail> Query(IQueryable<CustomerLabelDetail> details, Guid companyId) =>
        details.Where(x => companyId != Guid.Empty && x.IsActive && x.Header.IsActive &&
            x.Header.Customer.CompanyId == companyId && x.Header.Product.CompanyId == companyId &&
            x.FieldKey.Trim().ToUpper() == "NAME" && x.FieldValue != null && x.FieldValue.Trim() != "")
            .OrderByDescending(x => x.Header.UpdatedDate ?? x.Header.CreatedDate)
            .ThenByDescending(x => x.Header.CreatedDate).ThenByDescending(x => x.Header.Id)
            .ThenBy(x => x.LineNo).ThenBy(x => x.Id);
}
