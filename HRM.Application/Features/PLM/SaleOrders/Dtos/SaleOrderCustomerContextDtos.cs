namespace HRM.Application.Features.PLM.SaleOrders.Dtos;

/// <summary>
/// Dữ liệu điền mặc định cho header tạo SaleOrder sau khi FE chọn khách hàng.
/// </summary>
public sealed class SaleOrderCustomerContextDto
{
    public Guid CustomerId { get; init; }
    public string CustomerExternalIdSnapshot { get; init; } = string.Empty;
    public string CustomerNameSnapshot { get; init; } = string.Empty;
    public string? RegistrationNumber { get; init; }
    public string? CustomerGroup { get; init; }
    public bool IsLead { get; init; }
    public Guid? DefaultContactId { get; init; }
    public string? Receiver { get; init; }
    public string? PhoneSnapshot { get; init; }
    public Guid? DefaultAddressId { get; init; }
    public string? DeliveryAddress { get; init; }
    public string? PaymentType { get; init; }
    public string? ShippingMethod { get; init; }
    public string? Note { get; init; }
    public IReadOnlyList<SaleOrderCustomerContactDto> Contacts { get; init; } = [];
    public IReadOnlyList<SaleOrderCustomerAddressDto> Addresses { get; init; } = [];
}

public sealed class SaleOrderCustomerContactDto
{
    public Guid ContactId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public bool IsPrimary { get; init; }
}

public sealed class SaleOrderCustomerAddressDto
{
    public Guid AddressId { get; init; }
    public string AddressLine { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
}
