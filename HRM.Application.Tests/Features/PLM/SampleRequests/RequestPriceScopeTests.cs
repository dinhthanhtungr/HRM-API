using System.Reflection;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Pagination;
using HRM.Application.Features.PLM.SampleRequests.Commands.ConfirmCustomerPrice;
using HRM.Application.Features.PLM.SampleRequests.Dtos.SampleTrials;
using HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDailyWork;
using HRM.Application.Tests.Features.PLM.ProductionOrders;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;

namespace HRM.Application.Tests.Features.PLM.SampleRequests;

public sealed class RequestPriceScopeTests
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("otherCompany", false)]
    [InlineData("otherProduct", false)]
    [InlineData("inactive", false)]
    [InlineData("missingLab", false)]
    [InlineData("missingDate", false)]
    [InlineData("noPrice", false)]
    [InlineData("notAllowed", false)]
    public async Task AcceptanceUsesApprovedProductPriceInsteadOfSentOption(string sourceScope, bool allowed)
    {
        var user = new SampleTrialFeedbackCorrectionTests.User("SaleUser");
        var (db, store) = ProductionOrderTestStore.CreateContext<IPLMWriteDbContext>();
        var (crm, prices) = ProductionOrderTestStore.CreateContext<ICRMReadDbContext>();
        var record = new SampleRequest { SampleRequestId = Guid.NewGuid(), CompanyId = user.CompanyId!.Value,
            ManagerBy = user.EmployeeId!.Value, ProductId = Guid.NewGuid(), IsActive = true, InfoType = "ColorSampleSet",
            Status = "InProgress", UpdatedDate = DateTime.Today, RealPriceQuoteDate = DateTime.Today };
        store.Seed(nameof(db.SampleRequests), record);
        store.Seed(nameof(db.Employees), new Employee { EmployeeId = user.EmployeeId.Value, CompanyId = record.CompanyId, IsActive = true });
        var source = new Formula { FormulaId = Guid.NewGuid(), CompanyId = sourceScope == "otherCompany" ? Guid.NewGuid() : record.CompanyId,
            ProductId = sourceScope == "otherProduct" ? Guid.NewGuid() : record.ProductId, IsActive = sourceScope != "inactive", Status = "Approved" };
        store.Seed(nameof(db.Formulas), source);
        var version = new ProductPricingVersion { ProductPricingVersionId = Guid.NewGuid(), CompanyId = record.CompanyId,
            ProductId = record.ProductId, SourceFormulaId = source.FormulaId, Status = ProductPricingStatus.Approved,
            StandardSellingPrice = 100, Currency = "VND", IsActive = true, Version = 1 };
        prices.Seed(nameof(crm.ProductPricingVersions), version);
        var sentFormula = Guid.NewGuid();
        var sender = DispatchProxy.Create<ISender, ReadySender>();
        ((ReadySender)sender).Item = new() { SampleRequestId = record.SampleRequestId, FormulaId = sentFormula,
            CanConfirmCustomerPrice = sourceScope != "notAllowed", HasApprovedStandardPrice = sourceScope != "noPrice",
            LabFormulaConfirmedAt = sourceScope == "missingLab" ? null : DateTime.Today,
            RealPriceQuoteDate = sourceScope == "missingDate" ? null : DateTime.Today };
        var result = await new ConfirmCustomerPriceCommandHandler(db, user, new CurrentUserPermissionService(user),
            SampleTrialFeedbackCorrectionTests.Visibility.Create(user), sender, crm)
            .Handle(new() { SampleRequestId = record.SampleRequestId, ExpectedUpdatedDate = record.UpdatedDate,
                CustomerAcceptedAt = DateTime.Today, CustomerFeedbackNote = "Accepted request price" }, default);
        Assert.Equal(allowed, result.Success);
        if (allowed)
        {
            var audit = Assert.Single(store.Added.OfType<SampleRequestWorkflowEvent>(), e => e.EventType == "CustomerPriceAccepted");
            Assert.Equal(source.FormulaId, audit.FormulaId); // Historical evidence retained; not a gate on future options.
            Assert.Equal(version.ProductPricingVersionId, audit.ProductPricingVersionId);
            Assert.Null(record.FormulaId); // Price acceptance never chooses a sample option.
        }
        else Assert.Empty(store.Added.OfType<SampleRequestWorkflowEvent>());
        Assert.NotEqual(sentFormula, source.FormulaId);
    }

    public class ReadySender : DispatchProxy
    {
        public SampleRequestSampleTrialReportDto Item = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Assert.IsType<GetSampleRequestDailyWorkQuery>(args![0]);
            return Task.FromResult(new PagedResult<SampleRequestSampleTrialReportDto>([Item], 1, 1, 1));
        }
    }
}

