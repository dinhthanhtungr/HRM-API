using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.CustomerCare.Services;

internal static class CustomerProfileRules
{
    public static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static void NormalizePrimaryAddresses(
        IEnumerable<Address> addresses,
        Guid? preferredPrimaryId = null)
    {
        var active = addresses.Where(address => address.IsActive).OrderBy(address => address.AddressId).ToList();
        if (active.Count == 0)
        {
            return;
        }

        var primaryId = preferredPrimaryId.HasValue && active.Any(x => x.AddressId == preferredPrimaryId.Value)
            ? preferredPrimaryId.Value
            : active.FirstOrDefault(address => address.IsPrimary == true)?.AddressId ?? active[0].AddressId;

        foreach (var address in active)
        {
            var shouldBePrimary = address.AddressId == primaryId;
            if (address.IsPrimary != shouldBePrimary)
            {
                address.IsPrimary = shouldBePrimary;
            }
        }
    }

    public static void NormalizePrimaryContacts(
        IEnumerable<Contact> contacts,
        Guid? preferredPrimaryId = null)
    {
        var active = contacts.Where(contact => contact.IsActive).OrderBy(contact => contact.ContactId).ToList();
        if (active.Count == 0)
        {
            return;
        }

        var primaryId = preferredPrimaryId.HasValue && active.Any(x => x.ContactId == preferredPrimaryId.Value)
            ? preferredPrimaryId.Value
            : active.FirstOrDefault(contact => contact.IsPrimary == true)?.ContactId ?? active[0].ContactId;

        foreach (var contact in active)
        {
            var shouldBePrimary = contact.ContactId == primaryId;
            if (contact.IsPrimary != shouldBePrimary)
            {
                contact.IsPrimary = shouldBePrimary;
            }
        }
    }
}
