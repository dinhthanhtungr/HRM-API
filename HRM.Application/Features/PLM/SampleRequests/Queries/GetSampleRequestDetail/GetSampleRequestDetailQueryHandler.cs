using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Rules;
using HRM.Application.Commons.Authorization.PLM;
using HRM.Application.Features.Attachments.Services;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.PLM.Shared.Rules;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Common;
using HRM.Application.Features.PLM.SampleRequests.Dtos.Detail;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Queries.GetSampleRequestDetail;

internal sealed class GetSampleRequestDetailQueryHandler
    : IRequestHandler<GetSampleRequestDetailQuery, SampleRequestDetailDto?>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly IPLMFieldVisibilityService _fieldVisibility;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ICurrentUser _currentUser;

    public GetSampleRequestDetailQueryHandler(
        IPLMReadDbContext dbContext,
        IPLMFieldVisibilityService fieldVisibility,
        ICustomerVisibilityService visibilityService,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _fieldVisibility = fieldVisibility;
        _visibilityService = visibilityService;
        _currentUser = currentUser;
    }

    public async Task<SampleRequestDetailDto?> Handle(
        GetSampleRequestDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SampleRequestId == Guid.Empty)
        {
            return null;
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        // Sale được đọc detail của Sample Request nội bộ trong cùng công ty.
        // Ngoại lệ này chỉ nằm ở read handler, không mở rộng quyền PATCH/mutation.
        var detailScope = _currentUser.IsInRole(ApplicationRoles.Sales.SaleUser)
            ? scope with { CanViewInternalCustomer = true }
            : scope;
        string? saleOrderCustomerExternalId = null;

        if (request.ForSaleOrder)
        {
            if (request.CustomerId is not { } customerId || customerId == Guid.Empty)
            {
                return null;
            }

            saleOrderCustomerExternalId = await _dbContext.Customers
                .AsNoTracking()
                .Where(customer =>
                    customer.CustomerId == customerId &&
                    customer.CompanyId == scope.CompanyId &&
                    customer.IsActive == true)
                .Select(customer => customer.ExternalId)
                .FirstOrDefaultAsync(cancellationToken);

            if (saleOrderCustomerExternalId is null)
            {
                return null;
            }

            var canUseAllCustomersFormula = PLMCustomerRules.CanUseAllCustomersFormula(
                request.OrderType,
                PLMCustomerRules.IsInternalCustomerExternalId(saleOrderCustomerExternalId));
            detailScope = canUseAllCustomersFormula
                ? scope with { HasFullCustomerView = true, CanViewInternalCustomer = true }
                : scope with { CanViewInternalCustomer = true };
        }

        var sampleRequestSource = _dbContext.SampleRequests
            .Where(x =>
                x.SampleRequestId == request.SampleRequestId &&
                x.Product.IsActive &&
                x.Product.CompanyId == scope.CompanyId &&
                x.Customer.IsActive == true &&
                x.Customer.CompanyId == scope.CompanyId);

        if (request.ForSaleOrder)
        {
            var sampleSent = SampleRequestStatus.SampleSent.ToString();
            var completed = SampleRequestStatus.Completed.ToString();
            var isInternalCustomer = PLMCustomerRules.IsInternalCustomerExternalId(saleOrderCustomerExternalId);
            var allowsSampleSentFormula = PLMCustomerRules.AllowsSampleSentFormula(
                request.OrderType,
                isInternalCustomer);
            var canUseAllCustomersFormula = PLMCustomerRules.CanUseAllCustomersFormula(
                request.OrderType,
                isInternalCustomer);

            sampleRequestSource = sampleRequestSource.Where(x =>
                (allowsSampleSentFormula
                    ? x.Status == sampleSent || x.Status == completed
                    : x.Status == completed) &&
                (canUseAllCustomersFormula ||
                 x.CustomerId == request.CustomerId ||
                 x.Customer.ExternalId == InternalCustomerRules.InternalCustomerExternalId));
        }

        var sampleRequestQuery = _visibilityService.ApplySampleRequestVisibility(
            sampleRequestSource.AsNoTracking(),
            _dbContext.Customers.AsNoTracking(),
            detailScope);

        var detail = await sampleRequestQuery
            .AsNoTracking()
            .Select(x => new SampleRequestDetailDto
            {
                Hero = new SampleRequestDetailHeroDto
                {
                    SampleRequestId = x.SampleRequestId,
                    ExternalId = x.ExternalId,
                    Status = x.Status,
                    Title = x.Product.Name,
                    ProductCode = x.Product.Code,
                    ColourCode = x.Product.ColourCode,
                    ColourName = x.Product.ColourName,
                    ColourNameLabel = SampleRequestAdditiveHelper.ResolveDisplayName(x.Product.Additive),
                    CustomerExternalId = x.Customer.ExternalId,
                    CustomerName = x.Customer.CustomerName,
                    ManagerByName = x.ManagerByNavigation.FullName,
                    BranchName = x.Branch != null ? x.Branch.Name : null,
                    CompanyName = x.Company != null ? x.Company.Name : null,
                    CreatedDate = x.CreatedDate,
                    ExpectedDeliveryDate = x.ExpectedDeliveryDate,
                    IsActive = x.IsActive
                },
                QuickSummary = new SampleRequestDetailQuickSummaryDto
                {
                    ProductId = x.ProductId,
                    CustomerId = x.CustomerId,
                    CompanyId = x.CompanyId,
                    BranchId = x.BranchId,
                    ManagerBy = x.ManagerBy,
                    SelectedDevelopmentFormulaId = x.FormulaId,
                    AttachmentCollectionId = x.AttachmentCollectionId,
                    CategoryId = x.Product.CategoryId,
                    CategoryName = x.Product.Category != null ? x.Product.Category.Name : null,
                    RequestType = x.RequestType,
                    ExpectedQuantity = x.ExpectedQuantity,
                    SampleQuantity = x.SampleQuantity,
                    ExpectedPrice = x.ExpectedPrice,
                    Weight = x.Product.Weight,
                    UpdatedDate = x.UpdatedDate
                },
                SampleRequestRequirement = new SampleRequestDetailRequirementCardDto
                {
                    RequestType = x.RequestType,
                    ExpectedQuantity = x.ExpectedQuantity,
                    ExpectedPrice = x.ExpectedPrice,
                    SampleQuantity = x.SampleQuantity,
                    NumberDeliverySampleDate = x.NumberDeliverySampleDate,
                    Package = x.Package,
                    BagWeight = x.BagWeight,
                    CustomerProductCode = x.CustomerProductCode,
                    RequestDeliveryDate = x.RequestDeliveryDate,
                    ExpectedDeliveryDate = x.ExpectedDeliveryDate,
                    RealDeliveryDate = x.RealDeliveryDate,
                    RequestTestSampleDate = x.RequestTestSampleDate,
                    ResponseDeliveryDate = x.ResponseDeliveryDate,
                    ExpectedPriceQuoteDate = x.ExpectedPriceQuoteDate,
                    RealPriceQuoteDate = x.RealPriceQuoteDate
                },
                TechnicalRequirement = new SampleRequestDetailTechnicalRequirementCardDto
                {
                    InfoType = x.InfoType,
                    SaleComment = x.SaleComment,
                    AdditionalComment = x.AdditionalComment,
                    Requirement = x.Product.Requirement,
                    Additive = x.Product.Additive,
                    AdditiveLabel = SampleRequestAdditiveHelper.ResolveDisplayName(x.Product.Additive),
                    UsageRate = x.Product.UsageRate,
                    DeltaE = x.Product.DeltaE,
                    ExpiryType = x.Product.ExpiryType,
                    StorageCondition = x.Product.StorageCondition,
                    LabComment = x.Product.LabComment,
                    Procedure = x.Product.Procedure,
                    RecycleRate = x.Product.RecycleRate,
                    TaicalRate = x.Product.TaicalRate,
                    Application = x.Product.Application,
                    ProductUsage = x.Product.ProductUsage,
                    PolymerMatchedIn = x.Product.PolymerMatchedIn,
                    EndUser = x.Product.EndUser,    
                    FoodSafety = x.Product.FoodSafety,
                    RohsStandard = x.Product.RohsStandard,
                    ReachStandard = x.Product.ReachStandard,
                    MaxTemp = x.Product.MaxTemp,
                    WeatherResistance = x.Product.WeatherResistance,
                    LightCondition = x.Product.LightCondition,
                    VisualTest = x.Product.VisualTest,
                    ReturnSample = x.Product.ReturnSample,
                    IsRecycle = x.Product.IsRecycle,
                    GRS = x.Product.GRS,
                    GRSConsumerType = x.Product.GRSConsumerType,
                    OtherComment = x.OtherComment
                }
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (detail is null)
        {
            return null;
        }

        var canViewFormulaPrices = _fieldVisibility.CanViewFormulaPrices();

        detail.FormulaLookups = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x => x.IsActive && x.ProductId == detail.QuickSummary.ProductId)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new SampleRequestDetailFormulaLookupDto
            {
                FormulaId = x.FormulaId,
                ExternalId = x.ExternalId,
                Name = x.Name,
                Status = x.Status,
                TotalPrice = canViewFormulaPrices ? x.TotalPrice : null
            })
            .ToListAsync(cancellationToken);

        detail.QuickSummary.DevelopmentFormulaCount = detail.FormulaLookups.Count;

        detail.Attachments = await _dbContext.AttachmentModels
            .AsNoTracking()
            .Where(x => x.IsActive && x.AttachmentCollectionId == detail.QuickSummary.AttachmentCollectionId)
            .OrderBy(x => x.CreateDate)
            .Select(x => new SampleRequestAttachmentDto
            {
                AttachmentId = x.AttachmentId,
                AttachmentCollectionId = x.AttachmentCollectionId,
                Slot = x.Slot,
                FileName = x.FileName,
                SizeBytes = x.SizeBytes,
                Url = AttachmentFileHelper.BuildUrl(x.AttachmentId),
                DownloadUrl = AttachmentFileHelper.BuildDownloadUrl(x.AttachmentId),
                IsImage = AttachmentFileHelper.IsImageFile(x.FileName),
                CreateDate = x.CreateDate,
                CreateBy = x.CreateBy
            })
            .ToListAsync(cancellationToken);

        detail.QuickSummary.AttachmentCount = detail.Attachments.Count;

        return detail;
    }
}
