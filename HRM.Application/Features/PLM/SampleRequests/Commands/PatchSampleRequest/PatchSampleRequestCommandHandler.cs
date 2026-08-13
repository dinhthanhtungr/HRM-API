using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.CRM.Quotations.Services;
using HRM.Application.Features.PLM.SampleRequests.Commands;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.DataChangeRequests;
using HRM.Application.Features.PLM.SampleRequests.Services;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;

internal sealed class PatchSampleRequestCommandHandler
    : IRequestHandler<PatchSampleRequestCommand, OperationResult<Guid>>
{
    private const string DataChangeApprovalAuditReason = "SampleRequestDataChangeApproval";
    private const string DirectPatchAuditReason = "SampleRequestDirectPatch";

    private static readonly IReadOnlySet<string> SampleRequestClearFields = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "sample_request.expected_quantity",
        "sample_request.expected_price",
        "sample_request.sample_quantity",
        "sample_request.number_delivery_sample_date",
        "sample_request.customer_product_code",
        "sample_request.request_delivery_date",
        "sample_request.expected_delivery_date",
        "sample_request.real_delivery_date",
        "sample_request.request_test_sample_date",
        "sample_request.response_delivery_date",
        "sample_request.expected_price_quote_date",
        "sample_request.real_price_quote_date",
        "sample_request.info_type",
        "sample_request.other_comment",
        "sample_request.sale_comment",
        "sample_request.additional_comment",
        "sample_request.formula_id"
    };

    private static readonly IReadOnlySet<string> ProductClearFields = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "product.colour_code",
        "product.name",
        "product.colour_name",
        "product.additive",
        "product.usage_rate",
        "product.delta_e",
        "product.requirement",
        "product.expiry_type",
        "product.storage_condition",
        "product.lab_comment",
        "product.procedure",
        "product.recycle_rate",
        "product.taical_rate",
        "product.application",
        "product.product_usage",
        "product.polymer_matched_in",
        "product.code",
        "product.end_user",
        "product.food_safety",
        "product.rohs_standard",
        "product.reach_standard",
        "product.max_temp",
        "product.weather_resistance",
        "product.light_condition",
        "product.visual_test",
        "product.return_sample",
        "product.weight",
        "product.unit",
        "product.other_comment"
    };

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly SampleRequestConversationSubjectService _conversationSubjectService;
    private readonly DraftQuotationProductSnapshotSyncService _draftQuotationProductSnapshotSyncService;
    private readonly ISender _sender;

    public PatchSampleRequestCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        SampleRequestConversationSubjectService conversationSubjectService,
        DraftQuotationProductSnapshotSyncService draftQuotationProductSnapshotSyncService,
        ISender sender)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _conversationSubjectService = conversationSubjectService;
        _draftQuotationProductSnapshotSyncService = draftQuotationProductSnapshotSyncService;
        _sender = sender;
    }


    public async Task<OperationResult<Guid>> Handle(
        PatchSampleRequestCommand request,
        CancellationToken cancellationToken)
    {


        if (request.SampleRequestId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("SampleRequestId is invalid.");
        }

        var companyId = _currentUser.CompanyId;
        if (!companyId.HasValue || companyId.Value == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("Current company is invalid.");
        }

        var clearFields = NormalizeClearFields(request.ClearFields);
        var clearValidationError = ValidateClearFields(request, clearFields);
        if (clearValidationError is not null)
        {
            return OperationResult<Guid>.Fail(clearValidationError);
        }

        var sampleRequest = await _dbContext.SampleRequests
            .FirstOrDefaultAsync(
                x =>
                    x.SampleRequestId == request.SampleRequestId &&
                    x.CompanyId == companyId.Value &&
                    x.IsActive,
                cancellationToken);

        if (sampleRequest is null)
        {
            return OperationResult<Guid>.Fail("Sample request was not found.");
        }

        if (ShouldPatchProduct(request) && !CanPatchProductDirectly(_currentUser, sampleRequest))
        {
            return OperationResult<Guid>.Fail("You are not allowed to update product information directly for this sample request.");
        }

        if (IsStatus(request.Status, SampleRequestStatus.SampleSent) ||
            IsStatus(request.Status, SampleRequestStatus.Completed))
        {
            return OperationResult<Guid>.Fail(
                "Use the Formula send-sample action or sample-trial customer feedback action for this lifecycle status.");
        }

        var originalStatus = sampleRequest.Status;

        if (request.ExpectedUpdatedDate.HasValue &&
            sampleRequest.UpdatedDate.HasValue &&
            sampleRequest.UpdatedDate.Value.Ticks != request.ExpectedUpdatedDate.Value.Ticks)
        {
            return OperationResult<Guid>.Fail("Sample request was changed by another user. Please reload before saving.");
        }

        if (request.CustomerId.HasValue)
        {
            PatchHelper.SetGuidIfValid(
                request.CustomerId,
                () => sampleRequest.CustomerId,
                value => sampleRequest.CustomerId = value);
        }

        if (request.ManagerBy.HasValue)
        {
            PatchHelper.SetGuidIfValid(
                request.ManagerBy,
                () => sampleRequest.ManagerBy,
                value => sampleRequest.ManagerBy = value);
        }

        if (request.ProductId is { } productId && productId != Guid.Empty)
        {
            var productExists = await _dbContext.Products
                .AsNoTracking()
                .AnyAsync(x =>
                    x.ProductId == productId &&
                    x.CompanyId == companyId.Value &&
                    x.IsActive,
                    cancellationToken);

            if (!productExists)
            {
                return OperationResult<Guid>.Fail("Product does not exist or is inactive.");
            }

            sampleRequest.ProductId = productId;
        }

        if (request.FormulaId is { } formulaId && formulaId != Guid.Empty)
        {
            if (IsStatus(sampleRequest.Status, SampleRequestStatus.SampleSent))
            {
                return OperationResult<Guid>.Fail(
                    "Use sample-trial customer feedback to complete a SampleSent request. Formula cannot be selected directly.");
            }

            var formula = await _dbContext.Formulas
                .AsNoTracking()
                .Where(x =>
                    x.FormulaId == formulaId &&
                    x.CompanyId == companyId.Value &&
                    x.IsActive)
                .Select(x => new { x.FormulaId, x.ProductId })
                .FirstOrDefaultAsync(cancellationToken);

            if (formula is null)
            {
                return OperationResult<Guid>.Fail("Formula does not exist or is inactive.");
            }

            if (formula.ProductId != sampleRequest.ProductId)
            {
                return OperationResult<Guid>.Fail("Formula does not belong to this sample request product.");
            }

            sampleRequest.FormulaId = formula.FormulaId;
        }

        var auditCorrelationId = Guid.CreateVersion7();
        var auditChangedAt = DateTime.Now;
        var oldSampleRequestSnapshot = SampleRequestDataChangeAuditHelper.BuildSampleRequestAuditSnapshot(sampleRequest);

        if (request.IsDataChangeApproval)
        {
            ApplyApprovedSampleRequestPatch(sampleRequest, request);
            ApplySampleRequestClearFields(sampleRequest, clearFields);

            await SampleRequestDataChangeAuditHelper.AddSampleRequestAuditIfChangedAsync(
                _dbContext,
                sampleRequest,
                _currentUser.EmployeeId,
                auditChangedAt,
                auditCorrelationId,
                oldSampleRequestSnapshot,
                DataChangeApprovalAuditReason,
                cancellationToken);
        }
        else
        {
            ApplySampleRequestPatch(sampleRequest, request);
            ApplySampleRequestClearFields(sampleRequest, clearFields);
        }

        Product? patchedProduct = null;
        var productColourCodeChanged = false;

        // Xác định có phải cập nhật thông tin product không, rồi mới thao tác cập nhật
        if (ShouldPatchProduct(request))
        {
            var product = await _dbContext.Products
                .FirstOrDefaultAsync(
                    x =>
                        x.ProductId == sampleRequest.ProductId &&
                        x.CompanyId == companyId.Value &&
                        x.IsActive,
                    cancellationToken);

            if (product is null)
            {
                return OperationResult<Guid>.Fail("Product does not exist or is inactive.");
            }

            var oldProductSnapshot = SampleRequestDataChangeAuditHelper.BuildProductAuditSnapshot(product);
            var originalColourCode = product.ColourCode;

            ApplyProductPatch(product, request);
            ApplyProductClearFields(product, clearFields);

            if (request.ColourCode is not null)
            {
                var colourCodeResult = await SampleRequestColourCodeGenerator.ResolveAsync(
                    _dbContext,
                    request.ColourCode,
                    excludedProductId: product.ProductId,
                    currentColourCode: product.ColourCode,
                    cancellationToken);

                if (!colourCodeResult.Success)
                {
                    return OperationResult<Guid>.Fail(colourCodeResult.Message ?? "ColourCode is invalid.");
                }

                var resolvedColourCode = colourCodeResult.Data?.ColourCode;
                if (string.IsNullOrWhiteSpace(resolvedColourCode))
                {
                    return OperationResult<Guid>.Fail("ColourCode is invalid.");
                }

                product.ColourCode = resolvedColourCode;

                if (!string.IsNullOrWhiteSpace(colourCodeResult.Data?.AdditiveCode))
                {
                    product.Additive = colourCodeResult.Data.AdditiveCode;
                }
            }

            patchedProduct = product;
            productColourCodeChanged = !string.Equals(
                originalColourCode,
                product.ColourCode,
                StringComparison.Ordinal);

            if (!string.IsNullOrWhiteSpace(product.ColourCode))
            {
                var duplicated = await _dbContext.Products
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.ProductId != product.ProductId &&
                        x.ColourCode == product.ColourCode &&
                        x.IsActive,
                        cancellationToken);

                if (duplicated)
                {
                    return OperationResult<Guid>.Fail("ColourCode already exists.");
                }
            }

            if (request.IsDataChangeApproval)
            {
                await SampleRequestDataChangeAuditHelper.AddProductAuditIfChangedAsync(
                    _dbContext,
                    product,
                    _currentUser.EmployeeId,
                    auditChangedAt,
                    auditCorrelationId,
                    oldProductSnapshot,
                    DataChangeApprovalAuditReason,
                    cancellationToken);
            }
            else
            {
                await SampleRequestDataChangeAuditHelper.AddProductAuditIfChangedAsync(
                    _dbContext,
                    product,
                    _currentUser.EmployeeId,
                    auditChangedAt,
                    auditCorrelationId,
                    oldProductSnapshot,
                    DirectPatchAuditReason,
                    cancellationToken);
            }
        }

        var wasNew = IsStatus(sampleRequest.Status, SampleRequestStatus.New);

        ApplyStatusRules(sampleRequest, patchedProduct, request);

        var completedByFormulaSelection =
            request.FormulaId is { } completedFormulaId &&
            completedFormulaId != Guid.Empty &&
            IsStatus(originalStatus, SampleRequestStatus.SampleSent) &&
            IsStatus(sampleRequest.Status, SampleRequestStatus.Completed);

        var cancelledByPatch =
            !IsStatus(originalStatus, SampleRequestStatus.Cancelled) &&
            IsStatus(sampleRequest.Status, SampleRequestStatus.Cancelled);

        var startedProcessing = wasNew &&
            IsStatus(sampleRequest.Status, SampleRequestStatus.InProgress) &&
            patchedProduct is not null &&
            HasProductIdentity(patchedProduct);

        if (!request.IsDataChangeApproval)
        {
            await SampleRequestDataChangeAuditHelper.AddSampleRequestAuditIfChangedAsync(
                _dbContext,
                sampleRequest,
                _currentUser.EmployeeId,
                auditChangedAt,
                auditCorrelationId,
                oldSampleRequestSnapshot,
                DirectPatchAuditReason,
                cancellationToken);
        }

        sampleRequest.UpdatedBy = _currentUser.EmployeeId;
        sampleRequest.UpdatedDate = DateTime.Now;

        if (request.FormulaId is { } selectedFormulaId && selectedFormulaId != Guid.Empty)
        {
            await UpdateSelectedFormulaAsync(
                sampleRequest.ProductId,
                selectedFormulaId,
                completeSelectedFormula: completedByFormulaSelection,
                cancellationToken);
        }

        if (patchedProduct is not null || request.ProductId.HasValue)
        {
            var colourCode = patchedProduct?.ColourCode;
            if (colourCode is null && request.ProductId.HasValue)
            {
                colourCode = await _dbContext.Products
                    .AsNoTracking()
                    .Where(x =>
                        x.ProductId == sampleRequest.ProductId &&
                        x.CompanyId == companyId.Value &&
                        x.IsActive)
                    .Select(x => x.ColourCode)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            await _conversationSubjectService.SyncSubjectAsync(
                sampleRequest.SampleRequestId,
                sampleRequest.CompanyId,
                sampleRequest.ExternalId,
                colourCode,
                cancellationToken);
        }

        if (productColourCodeChanged && patchedProduct is not null)
        {
            await _draftQuotationProductSnapshotSyncService.SyncColourCodeAsync(
                patchedProduct.ProductId,
                companyId.Value,
                patchedProduct.ColourCode,
                _currentUser.EmployeeId,
                sampleRequest.UpdatedDate.Value,
                cancellationToken);
        }

        if (!request.DeferSaveChanges)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (startedProcessing)
            {
                var sendResult = await SendStartedProcessingMessageAsync(
                    sampleRequest.SampleRequestId,
                    sampleRequest.ExternalId,
                    patchedProduct!.ColourCode,
                    patchedProduct.Name,
                    cancellationToken);

                if (!sendResult.Success)
                {
                    return OperationResult<Guid>.Ok(
                        sampleRequest.SampleRequestId,
                        $"Updated sample request successfully, but could not send start-processing notification: {sendResult.Message}");
                }
            }

            if (completedByFormulaSelection)
            {
                var sendResult = await SendFormulaCompletedMessageAsync(
                    sampleRequest.SampleRequestId,
                    sampleRequest.ExternalId,
                    request.FormulaId!.Value,
                    cancellationToken);

                if (!sendResult.Success)
                {
                    return OperationResult<Guid>.Ok(
                        sampleRequest.SampleRequestId,
                        $"Updated sample request successfully, but could not send formula-completed notification: {sendResult.Message}");
                }
            }

            if (cancelledByPatch)
            {
                var sendResult = await SendCancelledMessageAsync(
                    sampleRequest.SampleRequestId,
                    sampleRequest.ExternalId,
                    cancellationToken);

                if (!sendResult.Success)
                {
                    return OperationResult<Guid>.Ok(
                        sampleRequest.SampleRequestId,
                        $"Updated sample request successfully, but could not send cancelled notification: {sendResult.Message}");
                }
            }
        }

        return OperationResult<Guid>.Ok(
            sampleRequest.SampleRequestId,
            "Updated sample request successfully.");
    }

    private static void ApplySampleRequestPatch(
        SampleRequest sampleRequest,
        PatchSampleRequestCommand request)
    {
        PatchTrimmedIfPresent(request.Status, () => sampleRequest.Status, value => sampleRequest.Status = value ?? string.Empty);
        PatchTrimmedIfPresent(request.RequestType, () => sampleRequest.RequestType, value => sampleRequest.RequestType = value ?? string.Empty);
        PatchNullableIfHasValue(request.ExpectedQuantity, () => sampleRequest.ExpectedQuantity, value => sampleRequest.ExpectedQuantity = value);
        PatchNullableIfHasValue(request.ExpectedPrice, () => sampleRequest.ExpectedPrice, value => sampleRequest.ExpectedPrice = value);
        PatchNullableIfHasValue(request.SampleQuantity, () => sampleRequest.SampleQuantity, value => sampleRequest.SampleQuantity = value);
        PatchNullableIfHasValue(request.NumberDeliverySampleDate, () => sampleRequest.NumberDeliverySampleDate, value => sampleRequest.NumberDeliverySampleDate = value);
        PatchTrimmedIfPresent(request.Package, () => sampleRequest.Package, value => sampleRequest.Package = value ?? string.Empty);
        PatchHelper.SetIfHasValue(request.BagWeight, () => sampleRequest.BagWeight, value => sampleRequest.BagWeight = value);
        PatchTrimmedIfPresent(request.CustomerProductCode, () => sampleRequest.CustomerProductCode, value => sampleRequest.CustomerProductCode = value);
        PatchNullableIfHasValue(request.RequestDeliveryDate, () => sampleRequest.RequestDeliveryDate, value => sampleRequest.RequestDeliveryDate = value);
        PatchNullableIfHasValue(request.ExpectedDeliveryDate, () => sampleRequest.ExpectedDeliveryDate, value => sampleRequest.ExpectedDeliveryDate = value);
        PatchNullableIfHasValue(request.RealDeliveryDate, () => sampleRequest.RealDeliveryDate, value => sampleRequest.RealDeliveryDate = value);
        PatchNullableIfHasValue(request.RequestTestSampleDate, () => sampleRequest.RequestTestSampleDate, value => sampleRequest.RequestTestSampleDate = value);
        PatchNullableIfHasValue(request.ResponseDeliveryDate, () => sampleRequest.ResponseDeliveryDate, value => sampleRequest.ResponseDeliveryDate = value);
        PatchNullableIfHasValue(request.ExpectedPriceQuoteDate, () => sampleRequest.ExpectedPriceQuoteDate, value => sampleRequest.ExpectedPriceQuoteDate = value);
        PatchNullableIfHasValue(request.RealPriceQuoteDate, () => sampleRequest.RealPriceQuoteDate, value => sampleRequest.RealPriceQuoteDate = value);
        PatchTrimmedIfPresent(request.InfoType, () => sampleRequest.InfoType, value => sampleRequest.InfoType = value);
        PatchTrimmedIfPresent(request.OtherComment, () => sampleRequest.OtherComment, value => sampleRequest.OtherComment = value);
        PatchTrimmedIfPresent(request.SaleComment, () => sampleRequest.SaleComment, value => sampleRequest.SaleComment = value);
        PatchTrimmedIfPresent(request.AdditionalComment, () => sampleRequest.AdditionalComment, value => sampleRequest.AdditionalComment = value);
    }

    private static void ApplyApprovedSampleRequestPatch(
        SampleRequest sampleRequest,
        PatchSampleRequestCommand request)
    {
        if (request.RequestType is not null)
        {
            PatchHelper.SetTrimmed(request.RequestType, () => sampleRequest.RequestType, value => sampleRequest.RequestType = value ?? string.Empty);
        }

        if (request.ExpectedQuantity.HasValue)
        {
            PatchHelper.SetNullable(request.ExpectedQuantity, () => sampleRequest.ExpectedQuantity, value => sampleRequest.ExpectedQuantity = value);
        }

        if (request.ExpectedPrice.HasValue)
        {
            PatchHelper.SetNullable(request.ExpectedPrice, () => sampleRequest.ExpectedPrice, value => sampleRequest.ExpectedPrice = value);
        }

        if (request.SampleQuantity.HasValue)
        {
            PatchHelper.SetNullable(request.SampleQuantity, () => sampleRequest.SampleQuantity, value => sampleRequest.SampleQuantity = value);
        }

        if (request.Package is not null)
        {
            PatchHelper.SetTrimmed(request.Package, () => sampleRequest.Package, value => sampleRequest.Package = value ?? string.Empty);
        }

        if (request.BagWeight.HasValue)
        {
            PatchHelper.SetIfHasValue(request.BagWeight, () => sampleRequest.BagWeight, value => sampleRequest.BagWeight = value);
        }

        if (request.CustomerProductCode is not null)
        {
            PatchHelper.SetTrimmed(request.CustomerProductCode, () => sampleRequest.CustomerProductCode, value => sampleRequest.CustomerProductCode = value);
        }

        if (request.RequestDeliveryDate.HasValue)
        {
            PatchHelper.SetNullable(request.RequestDeliveryDate, () => sampleRequest.RequestDeliveryDate, value => sampleRequest.RequestDeliveryDate = value);
        }

        if (request.ExpectedDeliveryDate.HasValue)
        {
            PatchHelper.SetNullable(request.ExpectedDeliveryDate, () => sampleRequest.ExpectedDeliveryDate, value => sampleRequest.ExpectedDeliveryDate = value);
        }

        if (request.RequestTestSampleDate.HasValue)
        {
            PatchHelper.SetNullable(request.RequestTestSampleDate, () => sampleRequest.RequestTestSampleDate, value => sampleRequest.RequestTestSampleDate = value);
        }

        if (request.ExpectedPriceQuoteDate.HasValue)
        {
            PatchHelper.SetNullable(request.ExpectedPriceQuoteDate, () => sampleRequest.ExpectedPriceQuoteDate, value => sampleRequest.ExpectedPriceQuoteDate = value);
        }

        if (request.InfoType is not null)
        {
            PatchHelper.SetTrimmed(request.InfoType, () => sampleRequest.InfoType, value => sampleRequest.InfoType = value);
        }

        if (request.OtherComment is not null)
        {
            PatchHelper.SetTrimmed(request.OtherComment, () => sampleRequest.OtherComment, value => sampleRequest.OtherComment = value);
        }

        if (request.SaleComment is not null)
        {
            PatchHelper.SetTrimmed(request.SaleComment, () => sampleRequest.SaleComment, value => sampleRequest.SaleComment = value);
        }

        if (request.AdditionalComment is not null)
        {
            PatchHelper.SetTrimmed(request.AdditionalComment, () => sampleRequest.AdditionalComment, value => sampleRequest.AdditionalComment = value);
        }
    }

    private void ApplyProductPatch(
        Product product,
        PatchSampleRequestCommand request)
    {
        PatchTrimmedIfPresent(request.ProductName, () => product.Name, value => product.Name = value);
        PatchTrimmedIfPresent(request.ColourName, () => product.ColourName, value => product.ColourName = value);
        PatchTrimmedIfPresent(request.Additive, () => product.Additive, value => product.Additive = value);
        if (!request.IsDataChangeApproval || request.UsageRate.HasValue)
        {
            PatchNullableIfHasValue(request.UsageRate, () => product.UsageRate, value => product.UsageRate = value);
        }
        PatchTrimmedIfPresent(request.DeltaE, () => product.DeltaE, value => product.DeltaE = value);
        PatchTrimmedIfPresent(request.ProductRequirement, () => product.Requirement, value => product.Requirement = value);
        PatchTrimmedIfPresent(request.ExpiryType, () => product.ExpiryType, value => product.ExpiryType = value);
        if (!request.IsDataChangeApproval || request.StorageCondition.HasValue)
        {
            PatchNullableIfHasValue(request.StorageCondition, () => product.StorageCondition, value => product.StorageCondition = value);
        }
        PatchTrimmedIfPresent(request.LabComment, () => product.LabComment, value => product.LabComment = value);
        PatchTrimmedIfPresent(request.Procedure, () => product.Procedure, value => product.Procedure = value);
        if (!request.IsDataChangeApproval || request.RecycleRate.HasValue)
        {
            PatchNullableIfHasValue(request.RecycleRate, () => product.RecycleRate, value => product.RecycleRate = value);
        }
        if (!request.IsDataChangeApproval || request.TaicalRate.HasValue)
        {
            PatchNullableIfHasValue(request.TaicalRate, () => product.TaicalRate, value => product.TaicalRate = value);
        }
        PatchTrimmedIfPresent(request.Application, () => product.Application, value => product.Application = value);
        PatchTrimmedIfPresent(request.ProductUsage, () => product.ProductUsage, value => product.ProductUsage = value);
        PatchTrimmedIfPresent(request.PolymerMatchedIn, () => product.PolymerMatchedIn, value => product.PolymerMatchedIn = value);
        PatchTrimmedIfPresent(request.ProductCode, () => product.Code, value => product.Code = value);
        PatchTrimmedIfPresent(request.EndUser, () => product.EndUser, value => product.EndUser = value);
        if (!request.IsDataChangeApproval || request.FoodSafety.HasValue)
        {
            PatchNullableIfHasValue(request.FoodSafety, () => product.FoodSafety, value => product.FoodSafety = value);
        }
        if (!request.IsDataChangeApproval || request.RohsStandard.HasValue)
        {
            PatchNullableIfHasValue(request.RohsStandard, () => product.RohsStandard, value => product.RohsStandard = value);
        }
        if (!request.IsDataChangeApproval || request.ReachStandard.HasValue)
        {
            PatchNullableIfHasValue(request.ReachStandard, () => product.ReachStandard, value => product.ReachStandard = value);
        }
        if (!request.IsDataChangeApproval || request.MaxTemp.HasValue)
        {
            PatchNullableIfHasValue(request.MaxTemp, () => product.MaxTemp, value => product.MaxTemp = value);
        }
        PatchTrimmedIfPresent(request.WeatherResistance, () => product.WeatherResistance, value => product.WeatherResistance = value);
        PatchTrimmedIfPresent(request.LightCondition, () => product.LightCondition, value => product.LightCondition = value);
        PatchTrimmedIfPresent(request.VisualTest, () => product.VisualTest, value => product.VisualTest = value);
        if (!request.IsDataChangeApproval || request.ReturnSample.HasValue)
        {
            PatchNullableIfHasValue(request.ReturnSample, () => product.ReturnSample, value => product.ReturnSample = value);
        }
        PatchHelper.SetIfHasValue(request.IsRecycle, () => product.IsRecycle, value => product.IsRecycle = value);
        PatchTrimmedIfPresent(request.ProductOtherComment, () => product.OtherComment, value => product.OtherComment = value);
        PatchHelper.SetGuidIfValid(request.CategoryId, () => product.CategoryId, value => product.CategoryId = value);
        if (!request.IsDataChangeApproval || request.Weight.HasValue)
        {
            PatchNullableIfHasValue(request.Weight, () => product.Weight, value => product.Weight = value);
        }
        PatchTrimmedIfPresent(request.Unit, () => product.Unit, value => product.Unit = value);

        var now = DateTime.Now;
        var employeeId = _currentUser.EmployeeId;

        if (employeeId.HasValue && !product.CreatedBy.HasValue)
        {
            product.CreatedBy = employeeId.Value;
            product.CreatedDate = now;
        }

        if (employeeId.HasValue)
        {
            product.UpdatedBy = employeeId.Value;
            product.UpdatedDate = now;
        }
    }

    private async Task UpdateSelectedFormulaAsync(
        Guid productId,
        Guid formulaId,
        bool completeSelectedFormula,
        CancellationToken cancellationToken)
    {
        var formulas = await _dbContext.Formulas
            .Where(x => x.ProductId == productId && x.IsActive)
            .ToListAsync(cancellationToken);

        var now = DateTime.Now;
        foreach (var formula in formulas)
        {
            formula.IsSelect = formula.FormulaId == formulaId;
            if (formula.FormulaId == formulaId && completeSelectedFormula)
            {
                formula.Status = FormulaStatus.Completed.ToString();
                formula.UpdatedBy = _currentUser.EmployeeId;
                formula.UpdatedDate = now;
            }
        }
    }




    // ================================================= Helper ======================================================

    private static void ApplyStatusRules(
    SampleRequest sampleRequest,
    Product? patchedProduct,
    PatchSampleRequestCommand request)
    {
        if (patchedProduct is not null &&
            IsStatus(sampleRequest.Status, SampleRequestStatus.New) &&
            HasProductIdentity(patchedProduct))
        {
            sampleRequest.Status = SampleRequestStatus.InProgress.ToString();
            return;
        }

        if (request.FormulaId is { } formulaId &&
            formulaId != Guid.Empty &&
            IsStatus(sampleRequest.Status, SampleRequestStatus.SampleSent))
        {
            sampleRequest.Status = SampleRequestStatus.Completed.ToString();
        }
    }

    /// <summary>
    /// Xác định lab thao tác trên product có đủ thông tin để xác định danh tính sản phẩm hay không.
    /// </summary>
    /// <param name="product"></param>
    /// <returns></returns>
    private static bool HasProductIdentity(Product product)
    {
        return !string.IsNullOrWhiteSpace(product.Name) &&
            !string.IsNullOrWhiteSpace(product.ColourCode);
    }

    private Task<OperationResult<SendInternalMessageResultDto>> SendStartedProcessingMessageAsync(
        Guid sampleRequestId,
        string sampleRequestExternalId,
        string? colourCode,
        string? productName,
        CancellationToken cancellationToken)
    {
        return _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = sampleRequestId,
            Type = SampleRequestNotificationType.GeneralMessage,
            Message = BuildStartedProcessingMessage(sampleRequestExternalId, colourCode, productName),
            TopicOverride = TopicNotifications.SampleRequestUpdated,
            TitleOverride = "Lab đã bắt đầu xử lý yêu cầu phối mẫu"
        }, cancellationToken);
    }

    private static string BuildStartedProcessingMessage(
        string sampleRequestExternalId,
        string? colourCode,
        string? productName)
    {
        var normalizedColourCode = string.IsNullOrWhiteSpace(colourCode) ? "Chưa có" : colourCode.Trim();
        var normalizedProductName = string.IsNullOrWhiteSpace(productName) ? "Chưa có" : productName.Trim();

        return $"Lab đã bắt đầu xử lý yêu cầu phối mẫu {sampleRequestExternalId}.\nMã màu: {normalizedColourCode}\nTên sản phẩm: {normalizedProductName}";
    }

    private async Task<OperationResult<SendInternalMessageResultDto>> SendFormulaCompletedMessageAsync(
        Guid sampleRequestId,
        string sampleRequestExternalId,
        Guid formulaId,
        CancellationToken cancellationToken)
    {
        var formulaExternalId = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x => x.FormulaId == formulaId)
            .Select(x => x.ExternalId)
            .FirstOrDefaultAsync(cancellationToken);

        var formulaCode = string.IsNullOrWhiteSpace(formulaExternalId)
            ? formulaId.ToString()
            : formulaExternalId.Trim();

        return await _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = sampleRequestId,
            Type = SampleRequestNotificationType.GeneralMessage,
            Message = $"Công thức {formulaCode} của yêu cầu phối mẫu {sampleRequestExternalId} đã hoàn thành, sẵn sàng cho báo giá.",
            TopicOverride = TopicNotifications.SampleRequestFormulaCompleted,
            TitleOverride = "Công thức hoàn thành, sẵn sàng cho báo giá"
        }, cancellationToken);
    }

    private Task<OperationResult<SendInternalMessageResultDto>> SendCancelledMessageAsync(
        Guid sampleRequestId,
        string sampleRequestExternalId,
        CancellationToken cancellationToken)
    {
        return _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = sampleRequestId,
            Type = SampleRequestNotificationType.GeneralMessage,
            Message = $"Yêu cầu phối mẫu {sampleRequestExternalId} đã được hủy hoặc khách hàng không tiếp tục làm mẫu.",
            TopicOverride = TopicNotifications.SampleRequestFormulaUpdateCancelled,
            TitleOverride = "Yêu cầu phối mẫu đã hủy"
        }, cancellationToken);
    }

    /// <summary>
    /// Xác định trạng thái hiện tại có khớp với trạng thái mong đợi hay không.
    /// </summary>
    /// <param name="currentStatus"></param>
    /// <param name="expectedStatus"></param>
    /// <returns></returns>
    private static bool IsStatus(string? currentStatus, SampleRequestStatus expectedStatus)
    {
        return string.Equals(
            currentStatus?.Trim(),
            expectedStatus.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool PatchNullableIfHasValue<T>(
        T? incoming,
        Func<T?> current,
        Action<T?> apply)
        where T : struct
    {
        return incoming.HasValue &&
            PatchHelper.SetNullable(incoming, current, apply);
    }

    private static bool PatchTrimmedIfPresent(
        string? incoming,
        Func<string?> current,
        Action<string?> apply)
    {
        return incoming is not null &&
            PatchHelper.SetTrimmed(incoming, current, apply);
    }

    private static IReadOnlySet<string> NormalizeClearFields(IReadOnlyList<string>? clearFields)
    {
        if (clearFields is null || clearFields.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return clearFields
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? ValidateClearFields(
        PatchSampleRequestCommand request,
        IReadOnlySet<string> clearFields)
    {
        foreach (var fieldCode in clearFields)
        {
            if (!SampleRequestClearFields.Contains(fieldCode) &&
                !ProductClearFields.Contains(fieldCode))
            {
                return $"Clear field '{fieldCode}' is not supported.";
            }

            if (HasValueForClearField(request, fieldCode))
            {
                return $"Field '{fieldCode}' cannot be sent with a value and clearFields at the same time.";
            }
        }

        return null;
    }

    private static bool HasValueForClearField(
        PatchSampleRequestCommand request,
        string fieldCode)
    {
        return fieldCode.ToLowerInvariant() switch
        {
            "sample_request.expected_quantity" => request.ExpectedQuantity.HasValue,
            "sample_request.expected_price" => request.ExpectedPrice.HasValue,
            "sample_request.sample_quantity" => request.SampleQuantity.HasValue,
            "sample_request.number_delivery_sample_date" => request.NumberDeliverySampleDate.HasValue,
            "sample_request.customer_product_code" => request.CustomerProductCode is not null,
            "sample_request.request_delivery_date" => request.RequestDeliveryDate.HasValue,
            "sample_request.expected_delivery_date" => request.ExpectedDeliveryDate.HasValue,
            "sample_request.real_delivery_date" => request.RealDeliveryDate.HasValue,
            "sample_request.request_test_sample_date" => request.RequestTestSampleDate.HasValue,
            "sample_request.response_delivery_date" => request.ResponseDeliveryDate.HasValue,
            "sample_request.expected_price_quote_date" => request.ExpectedPriceQuoteDate.HasValue,
            "sample_request.real_price_quote_date" => request.RealPriceQuoteDate.HasValue,
            "sample_request.info_type" => request.InfoType is not null,
            "sample_request.other_comment" => request.OtherComment is not null,
            "sample_request.sale_comment" => request.SaleComment is not null,
            "sample_request.additional_comment" => request.AdditionalComment is not null,
            "sample_request.formula_id" => request.FormulaId.HasValue,

            "product.colour_code" => request.ColourCode is not null,
            "product.name" => request.ProductName is not null,
            "product.colour_name" => request.ColourName is not null,
            "product.additive" => request.Additive is not null,
            "product.usage_rate" => request.UsageRate.HasValue,
            "product.delta_e" => request.DeltaE is not null,
            "product.requirement" => request.ProductRequirement is not null,
            "product.expiry_type" => request.ExpiryType is not null,
            "product.storage_condition" => request.StorageCondition.HasValue,
            "product.lab_comment" => request.LabComment is not null,
            "product.procedure" => request.Procedure is not null,
            "product.recycle_rate" => request.RecycleRate.HasValue,
            "product.taical_rate" => request.TaicalRate.HasValue,
            "product.application" => request.Application is not null,
            "product.product_usage" => request.ProductUsage is not null,
            "product.polymer_matched_in" => request.PolymerMatchedIn is not null,
            "product.code" => request.ProductCode is not null,
            "product.end_user" => request.EndUser is not null,
            "product.food_safety" => request.FoodSafety.HasValue,
            "product.rohs_standard" => request.RohsStandard.HasValue,
            "product.reach_standard" => request.ReachStandard.HasValue,
            "product.max_temp" => request.MaxTemp.HasValue,
            "product.weather_resistance" => request.WeatherResistance is not null,
            "product.light_condition" => request.LightCondition is not null,
            "product.visual_test" => request.VisualTest is not null,
            "product.return_sample" => request.ReturnSample.HasValue,
            "product.weight" => request.Weight.HasValue,
            "product.unit" => request.Unit is not null,
            "product.other_comment" => request.ProductOtherComment is not null,
            _ => false
        };
    }

    private static void ApplySampleRequestClearFields(
        SampleRequest sampleRequest,
        IReadOnlySet<string> clearFields)
    {
        foreach (var fieldCode in clearFields.Where(SampleRequestClearFields.Contains))
        {
            switch (fieldCode.ToLowerInvariant())
            {
                case "sample_request.expected_quantity":
                    PatchHelper.SetNullable<double>(null, () => sampleRequest.ExpectedQuantity, value => sampleRequest.ExpectedQuantity = value);
                    break;
                case "sample_request.expected_price":
                    PatchHelper.SetNullable<decimal>(null, () => sampleRequest.ExpectedPrice, value => sampleRequest.ExpectedPrice = value);
                    break;
                case "sample_request.sample_quantity":
                    PatchHelper.SetNullable<double>(null, () => sampleRequest.SampleQuantity, value => sampleRequest.SampleQuantity = value);
                    break;
                case "sample_request.number_delivery_sample_date":
                    PatchHelper.SetNullable<int>(null, () => sampleRequest.NumberDeliverySampleDate, value => sampleRequest.NumberDeliverySampleDate = value);
                    break;
                case "sample_request.customer_product_code":
                    PatchHelper.SetNullableRef<string>(null, () => sampleRequest.CustomerProductCode, value => sampleRequest.CustomerProductCode = value);
                    break;
                case "sample_request.request_delivery_date":
                    PatchHelper.SetNullable<DateTime>(null, () => sampleRequest.RequestDeliveryDate, value => sampleRequest.RequestDeliveryDate = value);
                    break;
                case "sample_request.expected_delivery_date":
                    PatchHelper.SetNullable<DateTime>(null, () => sampleRequest.ExpectedDeliveryDate, value => sampleRequest.ExpectedDeliveryDate = value);
                    break;
                case "sample_request.real_delivery_date":
                    PatchHelper.SetNullable<DateTime>(null, () => sampleRequest.RealDeliveryDate, value => sampleRequest.RealDeliveryDate = value);
                    break;
                case "sample_request.request_test_sample_date":
                    PatchHelper.SetNullable<DateTime>(null, () => sampleRequest.RequestTestSampleDate, value => sampleRequest.RequestTestSampleDate = value);
                    break;
                case "sample_request.response_delivery_date":
                    PatchHelper.SetNullable<DateTime>(null, () => sampleRequest.ResponseDeliveryDate, value => sampleRequest.ResponseDeliveryDate = value);
                    break;
                case "sample_request.expected_price_quote_date":
                    PatchHelper.SetNullable<DateTime>(null, () => sampleRequest.ExpectedPriceQuoteDate, value => sampleRequest.ExpectedPriceQuoteDate = value);
                    break;
                case "sample_request.real_price_quote_date":
                    PatchHelper.SetNullable<DateTime>(null, () => sampleRequest.RealPriceQuoteDate, value => sampleRequest.RealPriceQuoteDate = value);
                    break;
                case "sample_request.info_type":
                    PatchHelper.SetNullableRef<string>(null, () => sampleRequest.InfoType, value => sampleRequest.InfoType = value);
                    break;
                case "sample_request.other_comment":
                    PatchHelper.SetNullableRef<string>(null, () => sampleRequest.OtherComment, value => sampleRequest.OtherComment = value);
                    break;
                case "sample_request.sale_comment":
                    PatchHelper.SetNullableRef<string>(null, () => sampleRequest.SaleComment, value => sampleRequest.SaleComment = value);
                    break;
                case "sample_request.additional_comment":
                    PatchHelper.SetNullableRef<string>(null, () => sampleRequest.AdditionalComment, value => sampleRequest.AdditionalComment = value);
                    break;
                case "sample_request.formula_id":
                    PatchHelper.SetNullable<Guid>(null, () => sampleRequest.FormulaId, value => sampleRequest.FormulaId = value);
                    break;
            }
        }
    }

    private static void ApplyProductClearFields(
        Product product,
        IReadOnlySet<string> clearFields)
    {
        foreach (var fieldCode in clearFields.Where(ProductClearFields.Contains))
        {
            switch (fieldCode.ToLowerInvariant())
            {
                case "product.colour_code":
                    PatchHelper.SetNullableRef<string>(null, () => product.ColourCode, value => product.ColourCode = value);
                    break;
                case "product.name":
                    PatchHelper.SetNullableRef<string>(null, () => product.Name, value => product.Name = value);
                    break;
                case "product.colour_name":
                    PatchHelper.SetNullableRef<string>(null, () => product.ColourName, value => product.ColourName = value);
                    break;
                case "product.additive":
                    PatchHelper.SetNullableRef<string>(null, () => product.Additive, value => product.Additive = value);
                    break;
                case "product.usage_rate":
                    PatchHelper.SetNullable<double>(null, () => product.UsageRate, value => product.UsageRate = value);
                    break;
                case "product.delta_e":
                    PatchHelper.SetNullableRef<string>(null, () => product.DeltaE, value => product.DeltaE = value);
                    break;
                case "product.requirement":
                    PatchHelper.SetNullableRef<string>(null, () => product.Requirement, value => product.Requirement = value);
                    break;
                case "product.expiry_type":
                    PatchHelper.SetNullableRef<string>(null, () => product.ExpiryType, value => product.ExpiryType = value);
                    break;
                case "product.storage_condition":
                    PatchHelper.SetNullable<bool>(null, () => product.StorageCondition, value => product.StorageCondition = value);
                    break;
                case "product.lab_comment":
                    PatchHelper.SetNullableRef<string>(null, () => product.LabComment, value => product.LabComment = value);
                    break;
                case "product.procedure":
                    PatchHelper.SetNullableRef<string>(null, () => product.Procedure, value => product.Procedure = value);
                    break;
                case "product.recycle_rate":
                    PatchHelper.SetNullable<double>(null, () => product.RecycleRate, value => product.RecycleRate = value);
                    break;
                case "product.taical_rate":
                    PatchHelper.SetNullable<double>(null, () => product.TaicalRate, value => product.TaicalRate = value);
                    break;
                case "product.application":
                    PatchHelper.SetNullableRef<string>(null, () => product.Application, value => product.Application = value);
                    break;
                case "product.product_usage":
                    PatchHelper.SetNullableRef<string>(null, () => product.ProductUsage, value => product.ProductUsage = value);
                    break;
                case "product.polymer_matched_in":
                    PatchHelper.SetNullableRef<string>(null, () => product.PolymerMatchedIn, value => product.PolymerMatchedIn = value);
                    break;
                case "product.code":
                    PatchHelper.SetNullableRef<string>(null, () => product.Code, value => product.Code = value);
                    break;
                case "product.end_user":
                    PatchHelper.SetNullableRef<string>(null, () => product.EndUser, value => product.EndUser = value);
                    break;
                case "product.food_safety":
                    PatchHelper.SetNullable<bool>(null, () => product.FoodSafety, value => product.FoodSafety = value);
                    break;
                case "product.rohs_standard":
                    PatchHelper.SetNullable<bool>(null, () => product.RohsStandard, value => product.RohsStandard = value);
                    break;
                case "product.reach_standard":
                    PatchHelper.SetNullable<bool>(null, () => product.ReachStandard, value => product.ReachStandard = value);
                    break;
                case "product.max_temp":
                    PatchHelper.SetNullable<double>(null, () => product.MaxTemp, value => product.MaxTemp = value);
                    break;
                case "product.weather_resistance":
                    PatchHelper.SetNullableRef<string>(null, () => product.WeatherResistance, value => product.WeatherResistance = value);
                    break;
                case "product.light_condition":
                    PatchHelper.SetNullableRef<string>(null, () => product.LightCondition, value => product.LightCondition = value);
                    break;
                case "product.visual_test":
                    PatchHelper.SetNullableRef<string>(null, () => product.VisualTest, value => product.VisualTest = value);
                    break;
                case "product.return_sample":
                    PatchHelper.SetNullable<bool>(null, () => product.ReturnSample, value => product.ReturnSample = value);
                    break;
                case "product.weight":
                    PatchHelper.SetNullable<double>(null, () => product.Weight, value => product.Weight = value);
                    break;
                case "product.unit":
                    PatchHelper.SetNullableRef<string>(null, () => product.Unit, value => product.Unit = value);
                    break;
                case "product.other_comment":
                    PatchHelper.SetNullableRef<string>(null, () => product.OtherComment, value => product.OtherComment = value);
                    break;
            }
        }
    }

    /// <summary>
    /// Xác định có phải cập nhật thông tin product hay không
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    private static bool ShouldPatchProduct(PatchSampleRequestCommand request)
    {
        return HasProductClearField(request) ||
            request.ColourCode is not null ||
            request.ProductName is not null ||
            request.ColourName is not null ||
            request.Additive is not null ||
            request.UsageRate.HasValue ||
            request.DeltaE is not null ||
            request.ProductRequirement is not null ||
            request.ExpiryType is not null ||
            request.StorageCondition.HasValue ||
            request.Application is not null ||
            request.ProductUsage is not null ||
            request.PolymerMatchedIn is not null ||
            request.ProductCode is not null ||
            request.EndUser is not null ||
            request.FoodSafety.HasValue ||
            request.RohsStandard.HasValue ||
            request.ReachStandard.HasValue ||
            request.MaxTemp.HasValue ||
            request.WeatherResistance is not null ||
            request.LightCondition is not null ||
            request.VisualTest is not null ||
            request.ReturnSample.HasValue ||
            request.LabComment is not null ||
            request.Procedure is not null ||
            request.RecycleRate.HasValue ||
            request.TaicalRate.HasValue ||
            request.IsRecycle.HasValue ||
            request.CategoryId.HasValue ||
            request.Weight.HasValue ||
            request.Unit is not null ||
            request.ProductOtherComment is not null;
    }

    private static bool HasProductClearField(PatchSampleRequestCommand request)
    {
        return NormalizeClearFields(request.ClearFields)
            .Any(ProductClearFields.Contains);
    }

    private static bool CanPatchProductDirectly(
        ICurrentUser currentUser,
        SampleRequest sampleRequest)
    {
        if (currentUser.IsInAnyRole(ApplicationRoleSets.PLM.ProductTechnicalEditors))
        {
            return true;
        }

        var employeeId = currentUser.EmployeeId;
        return employeeId.HasValue &&
            employeeId.Value != Guid.Empty &&
            SampleRequestDataChangeAuthorization.CanRequest(currentUser) &&
            SampleRequestDataChangeAuthorization.CanRequestFor(
                currentUser,
                employeeId.Value,
                sampleRequest.ManagerBy,
                sampleRequest.CreatedBy);
    }
}
