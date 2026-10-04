using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Features.CRM.Quotations.Services;

internal static class QuotationSaveRules
{
    public static string? Validate(SaveQuotationRequest request)
    {
        if (!request.ExpectedUpdatedDate.HasValue || request.ExpectedUpdatedDate == default(DateTime))
            return "expectedUpdatedDate is required.";
        if (request.ClearFields is null)
            return "clearFields cannot be null; omit it or send an array.";
        if (request.Header?.ExpectedUpdatedDate is not null)
            return "Send expectedUpdatedDate only at the top level.";
        if (request.Header?.ContactId == Guid.Empty)
            return "Use clearFields to clear contactId; an empty GUID is invalid.";
        var textFields = new Dictionary<string, string?>
        {
            ["contactName"] = request.Header?.ContactName,
            ["contactPhone"] = request.Header?.ContactPhone,
            ["customerAddressSnapshot"] = request.Header?.CustomerAddressSnapshot,
            ["paymentTerms"] = request.Header?.PaymentTerms,
            ["deliveryTerms"] = request.Header?.DeliveryTerms,
            ["note"] = request.Header?.Note
        };
        foreach (var (field, value) in textFields)
            if (value is not null && string.IsNullOrWhiteSpace(value))
                return $"Use clearFields to clear {field}; a blank string is not a value.";
        if (request.Lines is not null && request.CustomerPriceLines is not null)
            return "Send either lines or customerPriceLines, not both.";
        if (request.Lines?.Any(x => x is null || x.PriceTiers is null || x.PriceTiers.Any(t => t is null)) == true ||
            request.CustomerPriceLines?.Any(x => x is null || x.PriceTiers is null || x.PriceTiers.Any(t => t is null)) == true ||
            request.Header?.Terms?.Any(x => x is null) == true)
            return "Lines, terms and price tiers cannot contain null items; priceTiers cannot be null.";
        if (request.Header is null && request.Lines is null && request.CustomerPriceLines is null && request.ClearFields.Count == 0)
            return "At least one update section or clear field is required.";
        if (request.ClearFields.Distinct(StringComparer.Ordinal).Count() != request.ClearFields.Count)
            return "clearFields must contain unique field names.";

        foreach (var field in request.ClearFields)
        {
            var header = request.Header;
            bool? hasValue = field switch
            {
                "contactId" => header?.ContactId is not null,
                "contactName" => header?.ContactName is not null,
                "contactPhone" => header?.ContactPhone is not null,
                "customerAddressSnapshot" => header?.CustomerAddressSnapshot is not null,
                "validUntil" => header?.ValidUntil is not null,
                "paymentTerms" => header?.PaymentTerms is not null,
                "deliveryTerms" => header?.DeliveryTerms is not null,
                "note" => header?.Note is not null,
                _ => null
            };
            if (hasValue is null) return $"clearFields contains unsupported field '{field}'.";
            if (hasValue.Value) return $"{field} cannot have a value and be listed in clearFields.";
        }
        return null;
    }

    public static string? ValidateStatus(Quotation quotation, SaveQuotationRequest request)
    {
        if (quotation.Status is not (QuotationStatus.Draft or QuotationStatus.PendingApproval or QuotationStatus.Approved))
            return "Only a draft, pending or approved quotation can be saved.";
        if (request.Lines is not null && quotation.Status != QuotationStatus.Draft)
            return "Only a draft quotation can have its lines replaced.";
        if (request.CustomerPriceLines is not null && !QuotationWorkflowRules.CanEditCustomerPricing(quotation.Status))
            return "Customer price tiers can only be edited while pricing is pending or approved.";
        var header = request.Header;
        if (quotation.Status != QuotationStatus.Draft && header is not null &&
            ((header.CustomerId.HasValue && header.CustomerId != quotation.CustomerId) ||
             (header.Currency is not null && !string.Equals(header.Currency.Trim(), quotation.Currency, StringComparison.OrdinalIgnoreCase)) ||
             (header.ExchangeRate.HasValue && header.ExchangeRate != quotation.ExchangeRate) ||
             (header.QuotationDate.HasValue && header.QuotationDate != quotation.QuotationDate)))
            return "Customer, currency, exchange rate and quotation date cannot be changed while pricing is pending or approved.";
        return null;
    }

    public static void Clear(Quotation quotation, IReadOnlyList<string> fields)
    {
        foreach (var field in fields)
        {
            switch (field)
            {
                case "contactId": quotation.ContactId = null; break;
                case "contactName": quotation.ContactName = null; break;
                case "contactPhone": quotation.ContactPhone = null; break;
                case "customerAddressSnapshot": quotation.CustomerAddressSnapshot = null; break;
                case "validUntil": quotation.ValidUntil = null; break;
                case "paymentTerms": quotation.PaymentTerms = null; break;
                case "deliveryTerms": quotation.DeliveryTerms = null; break;
                case "note": quotation.Note = null; break;
            }
        }
    }
}
