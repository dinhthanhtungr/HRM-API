using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Application.Tests.Features.CRM.Quotations;

public sealed class QuotationWorkflowRulesTests
{
    [Fact]
    public void Draft_AllowsPricingRequestAndSending()
    {
        Assert.True(QuotationWorkflowRules.CanRequestPricing(QuotationStatus.Draft));
        Assert.False(QuotationWorkflowRules.CanEditCustomerPricing(QuotationStatus.Draft));
        Assert.False(QuotationWorkflowRules.CanWithdrawPricingRequest(QuotationStatus.Draft));
        Assert.True(QuotationWorkflowRules.CanMarkSent(QuotationStatus.Draft));
    }

    [Fact]
    public void PendingApproval_AllowsCustomerPricingWithdrawalAndSending()
    {
        Assert.False(QuotationWorkflowRules.CanRequestPricing(QuotationStatus.PendingApproval));
        Assert.True(QuotationWorkflowRules.CanEditCustomerPricing(QuotationStatus.PendingApproval));
        Assert.True(QuotationWorkflowRules.CanWithdrawPricingRequest(QuotationStatus.PendingApproval));
        Assert.True(QuotationWorkflowRules.CanMarkSent(QuotationStatus.PendingApproval));
    }

    [Fact]
    public void Approved_AllowsCustomerPricingWithdrawalAndSending()
    {
        Assert.False(QuotationWorkflowRules.CanRequestPricing(QuotationStatus.Approved));
        Assert.True(QuotationWorkflowRules.CanEditCustomerPricing(QuotationStatus.Approved));
        Assert.True(QuotationWorkflowRules.CanWithdrawPricingRequest(QuotationStatus.Approved));
        Assert.True(QuotationWorkflowRules.CanMarkSent(QuotationStatus.Approved));
    }

    [Fact]
    public void Sent_IsImmutableInPricingWorkflow()
    {
        Assert.False(QuotationWorkflowRules.CanRequestPricing(QuotationStatus.Sent));
        Assert.False(QuotationWorkflowRules.CanEditCustomerPricing(QuotationStatus.Sent));
        Assert.False(QuotationWorkflowRules.CanWithdrawPricingRequest(QuotationStatus.Sent));
        Assert.False(QuotationWorkflowRules.CanMarkSent(QuotationStatus.Sent));
    }
}
