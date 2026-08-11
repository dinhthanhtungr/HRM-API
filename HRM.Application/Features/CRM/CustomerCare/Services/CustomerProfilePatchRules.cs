using HRM.Application.Commons.Patching;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Domain.Entities.CustomerSchema;

namespace HRM.Application.Features.CRM.CustomerCare.Services;

/// <summary>
/// Chuẩn hóa PATCH hồ sơ khách hàng: null là không đổi, còn xóa dữ liệu phải dùng clearFields hợp lệ.
/// </summary>
internal static class CustomerProfilePatchRules
{
    private static readonly IReadOnlySet<string> CustomerClearFields = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "customer.customer_group",
        "customer.application_name",
        "customer.registration_number",
        "customer.registration_address",
        "customer.tax_number",
        "customer.phone",
        "customer.website",
        "customer.issue_date",
        "customer.issued_place",
        "customer.fax_number"
    };

    private static readonly IReadOnlySet<string> AddressClearFields = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "address.address_line",
        "address.city",
        "address.district",
        "address.province",
        "address.country",
        "address.postal_code"
    };

    private static readonly IReadOnlySet<string> ContactClearFields = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "contact.first_name",
        "contact.last_name",
        "contact.gender",
        "contact.phone",
        "contact.email"
    };

    public static string? Validate(CustomerProfilePatchRequest request)
    {
        if (request.CustomerName is not null && string.IsNullOrWhiteSpace(request.CustomerName))
        {
            return "CustomerName cannot be empty.";
        }

        var topLevelError = ValidateClearFields(
            request.ClearFields,
            CustomerClearFields,
            fieldCode => HasCustomerValue(request, fieldCode));
        if (topLevelError is not null)
        {
            return topLevelError;
        }

        var valueError = ValidateOptionalStringValues(request);
        if (valueError is not null)
        {
            return valueError;
        }

        foreach (var address in request.Addresses ?? Array.Empty<UpdateCustomerAddressRequest>())
        {
            var addressError = ValidateClearFields(
                address.ClearFields,
                AddressClearFields,
                fieldCode => HasAddressValue(address, fieldCode));
            if (addressError is not null)
            {
                return addressError;
            }

            addressError = ValidateAddressStringValues(address);
            if (addressError is not null)
            {
                return addressError;
            }
        }

        foreach (var contact in request.Contacts ?? Array.Empty<UpdateCustomerContactRequest>())
        {
            var contactError = ValidateClearFields(
                contact.ClearFields,
                ContactClearFields,
                fieldCode => HasContactValue(contact, fieldCode));
            if (contactError is not null)
            {
                return contactError;
            }

            contactError = ValidateContactStringValues(contact);
            if (contactError is not null)
            {
                return contactError;
            }
        }

        return null;
    }

    public static void ApplyCustomerPatch(Customer customer, CustomerProfilePatchRequest request)
    {
        PatchHelper.SetTrimmed(request.CustomerName, () => customer.CustomerName, value => customer.CustomerName = value!);
        PatchHelper.SetTrimmed(request.CustomerGroup, () => customer.CustomerGroup, value => customer.CustomerGroup = value);
        PatchHelper.SetTrimmed(request.ApplicationName, () => customer.ApplicationName, value => customer.ApplicationName = value);
        PatchHelper.SetTrimmed(request.RegistrationNumber, () => customer.RegistrationNumber, value => customer.RegistrationNumber = value);
        PatchHelper.SetTrimmed(request.RegistrationAddress, () => customer.RegistrationAddress, value => customer.RegistrationAddress = value);
        PatchHelper.SetTrimmed(request.TaxNumber, () => customer.TaxNumber, value => customer.TaxNumber = value);
        PatchHelper.SetTrimmed(request.Phone, () => customer.Phone, value => customer.Phone = value);
        PatchHelper.SetTrimmed(request.Website, () => customer.Website, value => customer.Website = value);

        if (request.IssueDate.HasValue)
        {
            PatchHelper.SetNullable(request.IssueDate, () => customer.IssueDate, value => customer.IssueDate = value);
        }

        PatchHelper.SetTrimmed(request.IssuedPlace, () => customer.IssuedPlace, value => customer.IssuedPlace = value);
        PatchHelper.SetTrimmed(request.FaxNumber, () => customer.FaxNumber, value => customer.FaxNumber = value);

        foreach (var fieldCode in Normalize(request.ClearFields))
        {
            switch (fieldCode.ToLowerInvariant())
            {
                case "customer.customer_group":
                    PatchHelper.SetNullableRef<string>(null, () => customer.CustomerGroup, value => customer.CustomerGroup = value);
                    break;
                case "customer.application_name":
                    PatchHelper.SetNullableRef<string>(null, () => customer.ApplicationName, value => customer.ApplicationName = value);
                    break;
                case "customer.registration_number":
                    PatchHelper.SetNullableRef<string>(null, () => customer.RegistrationNumber, value => customer.RegistrationNumber = value);
                    break;
                case "customer.registration_address":
                    PatchHelper.SetNullableRef<string>(null, () => customer.RegistrationAddress, value => customer.RegistrationAddress = value);
                    break;
                case "customer.tax_number":
                    PatchHelper.SetNullableRef<string>(null, () => customer.TaxNumber, value => customer.TaxNumber = value);
                    break;
                case "customer.phone":
                    PatchHelper.SetNullableRef<string>(null, () => customer.Phone, value => customer.Phone = value);
                    break;
                case "customer.website":
                    PatchHelper.SetNullableRef<string>(null, () => customer.Website, value => customer.Website = value);
                    break;
                case "customer.issue_date":
                    PatchHelper.SetNullable<DateTime>(null, () => customer.IssueDate, value => customer.IssueDate = value);
                    break;
                case "customer.issued_place":
                    PatchHelper.SetNullableRef<string>(null, () => customer.IssuedPlace, value => customer.IssuedPlace = value);
                    break;
                case "customer.fax_number":
                    PatchHelper.SetNullableRef<string>(null, () => customer.FaxNumber, value => customer.FaxNumber = value);
                    break;
            }
        }
    }

    public static void ApplyAddressPatch(Address address, UpdateCustomerAddressRequest request)
    {
        PatchHelper.SetTrimmed(request.AddressLine, () => address.AddressLine, value => address.AddressLine = value);
        PatchHelper.SetTrimmed(request.City, () => address.City, value => address.City = value);
        PatchHelper.SetTrimmed(request.District, () => address.District, value => address.District = value);
        PatchHelper.SetTrimmed(request.Province, () => address.Province, value => address.Province = value);
        PatchHelper.SetTrimmed(request.Country, () => address.Country, value => address.Country = value);
        PatchHelper.SetTrimmed(request.PostalCode, () => address.PostalCode, value => address.PostalCode = value);

        foreach (var fieldCode in Normalize(request.ClearFields))
        {
            switch (fieldCode.ToLowerInvariant())
            {
                case "address.address_line":
                    PatchHelper.SetNullableRef<string>(null, () => address.AddressLine, value => address.AddressLine = value);
                    break;
                case "address.city":
                    PatchHelper.SetNullableRef<string>(null, () => address.City, value => address.City = value);
                    break;
                case "address.district":
                    PatchHelper.SetNullableRef<string>(null, () => address.District, value => address.District = value);
                    break;
                case "address.province":
                    PatchHelper.SetNullableRef<string>(null, () => address.Province, value => address.Province = value);
                    break;
                case "address.country":
                    PatchHelper.SetNullableRef<string>(null, () => address.Country, value => address.Country = value);
                    break;
                case "address.postal_code":
                    PatchHelper.SetNullableRef<string>(null, () => address.PostalCode, value => address.PostalCode = value);
                    break;
            }
        }
    }

    public static void ApplyContactPatch(Contact contact, UpdateCustomerContactRequest request)
    {
        PatchHelper.SetTrimmed(request.FirstName, () => contact.FirstName, value => contact.FirstName = value);
        PatchHelper.SetTrimmed(request.LastName, () => contact.LastName, value => contact.LastName = value);
        PatchHelper.SetTrimmed(request.Gender, () => contact.Gender, value => contact.Gender = value);
        PatchHelper.SetTrimmed(request.Phone, () => contact.Phone, value => contact.Phone = value);
        PatchHelper.SetTrimmed(request.Email, () => contact.Email, value => contact.Email = value);

        foreach (var fieldCode in Normalize(request.ClearFields))
        {
            switch (fieldCode.ToLowerInvariant())
            {
                case "contact.first_name":
                    PatchHelper.SetNullableRef<string>(null, () => contact.FirstName, value => contact.FirstName = value);
                    break;
                case "contact.last_name":
                    PatchHelper.SetNullableRef<string>(null, () => contact.LastName, value => contact.LastName = value);
                    break;
                case "contact.gender":
                    PatchHelper.SetNullableRef<string>(null, () => contact.Gender, value => contact.Gender = value);
                    break;
                case "contact.phone":
                    PatchHelper.SetNullableRef<string>(null, () => contact.Phone, value => contact.Phone = value);
                    break;
                case "contact.email":
                    PatchHelper.SetNullableRef<string>(null, () => contact.Email, value => contact.Email = value);
                    break;
            }
        }
    }

    private static string? ValidateClearFields(
        IReadOnlyList<string>? requestedFields,
        IReadOnlySet<string> supportedFields,
        Func<string, bool> hasValue)
    {
        foreach (var fieldCode in Normalize(requestedFields))
        {
            if (!supportedFields.Contains(fieldCode))
            {
                return $"Clear field '{fieldCode}' is not supported.";
            }

            if (hasValue(fieldCode))
            {
                return $"Field '{fieldCode}' cannot be sent with a value and clearFields at the same time.";
            }
        }

        return null;
    }

    private static IReadOnlySet<string> Normalize(IReadOnlyList<string>? fields)
        => fields is null || fields.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : fields
                .Where(field => !string.IsNullOrWhiteSpace(field))
                .Select(field => field.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool HasCustomerValue(CustomerProfilePatchRequest request, string fieldCode)
        => fieldCode.ToLowerInvariant() switch
        {
            "customer.customer_group" => request.CustomerGroup is not null,
            "customer.application_name" => request.ApplicationName is not null,
            "customer.registration_number" => request.RegistrationNumber is not null,
            "customer.registration_address" => request.RegistrationAddress is not null,
            "customer.tax_number" => request.TaxNumber is not null,
            "customer.phone" => request.Phone is not null,
            "customer.website" => request.Website is not null,
            "customer.issue_date" => request.IssueDate.HasValue,
            "customer.issued_place" => request.IssuedPlace is not null,
            "customer.fax_number" => request.FaxNumber is not null,
            _ => false
        };

    private static bool HasAddressValue(UpdateCustomerAddressRequest request, string fieldCode)
        => fieldCode.ToLowerInvariant() switch
        {
            "address.address_line" => request.AddressLine is not null,
            "address.city" => request.City is not null,
            "address.district" => request.District is not null,
            "address.province" => request.Province is not null,
            "address.country" => request.Country is not null,
            "address.postal_code" => request.PostalCode is not null,
            _ => false
        };

    private static bool HasContactValue(UpdateCustomerContactRequest request, string fieldCode)
        => fieldCode.ToLowerInvariant() switch
        {
            "contact.first_name" => request.FirstName is not null,
            "contact.last_name" => request.LastName is not null,
            "contact.gender" => request.Gender is not null,
            "contact.phone" => request.Phone is not null,
            "contact.email" => request.Email is not null,
            _ => false
        };

    private static string? ValidateOptionalStringValues(CustomerProfilePatchRequest request)
        => FirstBlankField(
            (request.CustomerGroup, "customerGroup"),
            (request.ApplicationName, "applicationName"),
            (request.RegistrationNumber, "registrationNumber"),
            (request.RegistrationAddress, "registrationAddress"),
            (request.TaxNumber, "taxNumber"),
            (request.Phone, "phone"),
            (request.Website, "website"),
            (request.IssuedPlace, "issuedPlace"),
            (request.FaxNumber, "faxNumber"));

    private static string? ValidateAddressStringValues(UpdateCustomerAddressRequest request)
        => FirstBlankField(
            (request.AddressLine, "address.addressLine"),
            (request.City, "address.city"),
            (request.District, "address.district"),
            (request.Province, "address.province"),
            (request.Country, "address.country"),
            (request.PostalCode, "address.postalCode"));

    private static string? ValidateContactStringValues(UpdateCustomerContactRequest request)
        => FirstBlankField(
            (request.FirstName, "contact.firstName"),
            (request.LastName, "contact.lastName"),
            (request.Gender, "contact.gender"),
            (request.Phone, "contact.phone"),
            (request.Email, "contact.email"));

    private static string? FirstBlankField(params (string? Value, string Name)[] fields)
    {
        var blankField = fields.FirstOrDefault(field => field.Value is not null && string.IsNullOrWhiteSpace(field.Value));
        return blankField.Name is null
            ? null
            : $"Field '{blankField.Name}' cannot be blank; use clearFields to remove it.";
    }
}
