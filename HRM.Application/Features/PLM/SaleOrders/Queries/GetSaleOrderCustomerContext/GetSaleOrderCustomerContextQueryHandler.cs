using HRM.Application.Abstractions.Persistence.PLM.SaleOrders;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.SaleOrders.Dtos;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Domain.Enums.Merchadises;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SaleOrders.Queries.GetSaleOrderCustomerContext;

/// <summary>
/// Áp dụng customer visibility, chọn contact/address primary hoặc phần tử active đầu tiên,
/// và lấy payment, shipping, note từ SaleOrder hợp lệ gần nhất.
/// </summary>
internal sealed class GetSaleOrderCustomerContextQueryHandler
    : IRequestHandler<GetSaleOrderCustomerContextQuery, SaleOrderCustomerContextDto?>
{
    private readonly ISaleOrderDbContext _dbContext;
    private readonly ICustomerVisibilityService _customerVisibilityService;

    public GetSaleOrderCustomerContextQueryHandler(
        ISaleOrderDbContext dbContext,
        ICustomerVisibilityService customerVisibilityService)
    {
        _dbContext = dbContext;
        _customerVisibilityService = customerVisibilityService;
    }

    public async Task<SaleOrderCustomerContextDto?> Handle(
        GetSaleOrderCustomerContextQuery request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return null;
        }

        var scope = await _customerVisibilityService.BuildScopeAsync(cancellationToken);
        var cancelledStatus = MerchadiseStatus.Cancelled.ToString();
        var visibleCustomerIds = _customerVisibilityService.ApplyCustomerVisibility(
            _dbContext.Customers.AsNoTracking(),
            scope)
            .Select(x => x.CustomerId);
        var customerQuery = _dbContext.Customers
            .AsNoTracking()
            .Where(customer =>
                customer.CompanyId == scope.CompanyId &&
                customer.IsActive == true &&
                (visibleCustomerIds.Contains(customer.CustomerId) ||
                 customer.ExternalId == PLMCustomerRules.InternalCustomerExternalId));

        return await customerQuery
            .Where(customer => customer.CustomerId == request.CustomerId)
            .Select(customer => new SaleOrderCustomerContextDto
            {
                CustomerId = customer.CustomerId,
                CustomerExternalIdSnapshot = customer.ExternalId,
                CustomerNameSnapshot = customer.CustomerName,
                RegistrationNumber = customer.RegistrationNumber,
                CustomerGroup = customer.CustomerGroup,
                IsLead = customer.IsLead,
                DefaultContactId = customer.Contacts
                    .Where(contact => contact.IsActive)
                    .OrderByDescending(contact => contact.IsPrimary == true)
                    .ThenBy(contact => contact.ContactId)
                    .Select(contact => (Guid?)contact.ContactId)
                    .FirstOrDefault(),
                Receiver = customer.Contacts
                    .Where(contact => contact.IsActive)
                    .OrderByDescending(contact => contact.IsPrimary == true)
                    .ThenBy(contact => contact.ContactId)
                    .Select(contact => (contact.FirstName + " " + contact.LastName).Trim())
                    .FirstOrDefault(),
                PhoneSnapshot = customer.Contacts
                    .Where(contact => contact.IsActive)
                    .OrderByDescending(contact => contact.IsPrimary == true)
                    .ThenBy(contact => contact.ContactId)
                    .Select(contact => contact.Phone)
                    .FirstOrDefault() ?? customer.Phone,
                DefaultAddressId = customer.Addresses
                    .Where(address => address.IsActive)
                    .OrderByDescending(address => address.IsPrimary == true)
                    .ThenBy(address => address.AddressId)
                    .Select(address => (Guid?)address.AddressId)
                    .FirstOrDefault(),
                DeliveryAddress = customer.Addresses
                    .Where(address => address.IsActive)
                    .OrderByDescending(address => address.IsPrimary == true)
                    .ThenBy(address => address.AddressId)
                    .Select(address => address.AddressLine)
                    .FirstOrDefault(),
                PaymentType = customer.MerchandiseOrders
                    .Where(order => order.IsActive && order.Status != cancelledStatus)
                    .OrderByDescending(order => order.CreateDate)
                    .ThenByDescending(order => order.MerchandiseOrderId)
                    .Select(order => order.PaymentType)
                    .FirstOrDefault(),
                ShippingMethod = customer.MerchandiseOrders
                    .Where(order => order.IsActive && order.Status != cancelledStatus)
                    .OrderByDescending(order => order.CreateDate)
                    .ThenByDescending(order => order.MerchandiseOrderId)
                    .Select(order => order.ShippingMethod)
                    .FirstOrDefault(),
                Note = customer.MerchandiseOrders
                    .Where(order => order.IsActive && order.Status != cancelledStatus)
                    .OrderByDescending(order => order.CreateDate)
                    .ThenByDescending(order => order.MerchandiseOrderId)
                    .Select(order => order.Note)
                    .FirstOrDefault(),
                Contacts = customer.Contacts
                    .Where(contact => contact.IsActive)
                    .OrderByDescending(contact => contact.IsPrimary == true)
                    .ThenBy(contact => contact.ContactId)
                    .Select(contact => new SaleOrderCustomerContactDto
                    {
                        ContactId = contact.ContactId,
                        Name = (contact.FirstName + " " + contact.LastName).Trim(),
                        Phone = contact.Phone,
                        Email = contact.Email,
                        IsPrimary = contact.IsPrimary == true
                    })
                    .ToList(),
                Addresses = customer.Addresses
                    .Where(address => address.IsActive && address.AddressLine != null)
                    .OrderByDescending(address => address.IsPrimary == true)
                    .ThenBy(address => address.AddressId)
                    .Select(address => new SaleOrderCustomerAddressDto
                    {
                        AddressId = address.AddressId,
                        AddressLine = address.AddressLine!,
                        IsPrimary = address.IsPrimary == true
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
