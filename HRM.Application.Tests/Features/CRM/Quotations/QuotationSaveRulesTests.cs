using HRM.Application.Features.CRM.Quotations.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationSaveRulesTests
{
    private static readonly DateTime Token = new(2026, 10, 4, 0, 0, 0);

    [Fact]
    public void SaveRequiresConcurrencyToken()
        => Assert.NotNull(QuotationSaveRules.Validate(new SaveQuotationRequest { Lines = [] }));

    [Fact]
    public void TwoLineModesCannotBeCombined()
        => Assert.NotNull(QuotationSaveRules.Validate(new SaveQuotationRequest
        { ExpectedUpdatedDate = Token, Lines = [], CustomerPriceLines = [] }));

    [Theory]
    [InlineData("companyId")]
    [InlineData("status")]
    [InlineData("updatedDate")]
    public void ServerOwnedFieldsCannotBeCleared(string field)
        => Assert.NotNull(QuotationSaveRules.Validate(new SaveQuotationRequest
        { ExpectedUpdatedDate = Token, ClearFields = [field] }));

    [Fact]
    public void ClearingAndSettingSameFieldIsRejected()
        => Assert.NotNull(QuotationSaveRules.Validate(new SaveQuotationRequest
        {
            ExpectedUpdatedDate = Token,
            Header = new UpdateQuotationRequest { Note = "new note" },
            ClearFields = ["note"]
        }));

    [Fact]
    public void BlankNullableHeaderMustUseExplicitClear()
        => Assert.NotNull(QuotationSaveRules.Validate(new SaveQuotationRequest
        { ExpectedUpdatedDate = Token, Header = new UpdateQuotationRequest { Note = " " } }));

    [Fact]
    public void ExplicitClearDoesNotChangeUnlistedFields()
    {
        var quotation = new Quotation { Note = "remove", PaymentTerms = "keep", ContactName = "keep" };
        QuotationSaveRules.Clear(quotation, ["note"]);
        Assert.Null(quotation.Note);
        Assert.Equal("keep", quotation.PaymentTerms);
        Assert.Equal("keep", quotation.ContactName);
    }

    [Theory]
    [InlineData(QuotationStatus.PendingApproval)]
    [InlineData(QuotationStatus.Approved)]
    [InlineData(QuotationStatus.Sent)]
    public void FullLineReplacementIsRejectedOutsideDraft(QuotationStatus status)
        => Assert.NotNull(QuotationSaveRules.ValidateStatus(new Quotation { Status = status },
            new SaveQuotationRequest { ExpectedUpdatedDate = Token, Lines = [] }));

    [Fact]
    public void PendingHeaderMayEchoUnchangedLockedFields()
    {
        var customerId = Guid.NewGuid();
        var quotation = new Quotation
        { Status = QuotationStatus.PendingApproval, CustomerId = customerId, Currency = "VND", ExchangeRate = 1 };
        var request = new SaveQuotationRequest
        {
            ExpectedUpdatedDate = Token,
            Header = new UpdateQuotationRequest { CustomerId = customerId, Currency = "vnd", ExchangeRate = 1 }
        };
        Assert.Null(QuotationSaveRules.ValidateStatus(quotation, request));
        Assert.NotNull(QuotationSaveRules.ValidateStatus(quotation,
            new SaveQuotationRequest { Header = new UpdateQuotationRequest { CustomerId = Guid.NewGuid() } }));
    }

    [Fact]
    public void EmptyDraftLinesExplicitlyReplaceAndNullSectionsAreUnchanged()
        => Assert.Null(QuotationSaveRules.Validate(new SaveQuotationRequest
        { ExpectedUpdatedDate = Token, Lines = [] }));

    [Fact]
    public void NullLineItemsAreRejectedBeforeStaging()
        => Assert.NotNull(QuotationSaveRules.Validate(new SaveQuotationRequest
        { ExpectedUpdatedDate = Token, Lines = [null!] }));
}
