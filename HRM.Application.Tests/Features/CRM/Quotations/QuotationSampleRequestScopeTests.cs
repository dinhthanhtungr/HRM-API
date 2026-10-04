using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationSampleRequestScopeTests
{
    [Theory]
    [InlineData(true, "KH_CUSTOMER", true, true, true, true)]
    [InlineData(false, "KH_VIETAUS", true, true, true, true)]
    [InlineData(false, "KH_OTHER", true, true, true, false)]
    [InlineData(false, "KH_VIETAUS", false, true, true, false)]
    [InlineData(false, "KH_VIETAUS", true, false, true, false)]
    [InlineData(false, "KH_VIETAUS", true, true, false, false)]
    public void Scope_OnlyAllowsOwnCustomerOrInternalSamplesWithinCompany(
        bool ownCustomer, string externalId, bool sameCompany,
        bool active, bool customerInCompany, bool expected)
    {
        var companyId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var sample = new SampleRequest
        {
            SampleRequestId = Guid.NewGuid(),
            CompanyId = sameCompany ? companyId : Guid.NewGuid(),
            CustomerId = ownCustomer ? customerId : Guid.NewGuid(),
            IsActive = active,
            Customer = new Customer
            {
                CompanyId = customerInCompany ? companyId : Guid.NewGuid(),
                ExternalId = externalId
            }
        };

        var result = QuotationLineBuilder.ScopeSampleRequests(
            new[] { sample }.AsQueryable(), companyId, customerId).ToList();

        if (expected)
            Assert.Same(sample, Assert.Single(result));
        else
            Assert.Empty(result);
    }
}
