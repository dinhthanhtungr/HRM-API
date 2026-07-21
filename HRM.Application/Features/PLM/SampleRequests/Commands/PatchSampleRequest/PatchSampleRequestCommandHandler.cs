using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Commons.Patching;
using HRM.Application.Features.PLM.SampleRequests.Commands;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.PatchSampleRequest;

internal sealed class PatchSampleRequestCommandHandler
    : IRequestHandler<PatchSampleRequestCommand, OperationResult<Guid>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public PatchSampleRequestCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }


    public async Task<OperationResult<Guid>> Handle(
        PatchSampleRequestCommand request,
        CancellationToken cancellationToken)
    {


        if (request.SampleRequestId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("SampleRequestId is invalid.");
        }

        var sampleRequest = await _dbContext.SampleRequests
            .FirstOrDefaultAsync(
                x => x.SampleRequestId == request.SampleRequestId && x.IsActive,
                cancellationToken);

        if (sampleRequest is null)
        {
            return OperationResult<Guid>.Fail("Sample request was not found.");
        }

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
                .AnyAsync(x => x.ProductId == productId && x.IsActive, cancellationToken);

            if (!productExists)
            {
                return OperationResult<Guid>.Fail("Product does not exist or is inactive.");
            }

            sampleRequest.ProductId = productId;
        }

        if (request.FormulaId is { } formulaId && formulaId != Guid.Empty)
        {
            var formula = await _dbContext.Formulas
                .AsNoTracking()
                .Where(x => x.FormulaId == formulaId && x.IsActive)
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

        ApplySampleRequestPatch(sampleRequest, request);

        Product? patchedProduct = null;

        // Xác định có phải cập nhật thông tin product không, rồi mới thao tác cập nhật
        if (ShouldPatchProduct(request))
        {
            var product = await _dbContext.Products
                .FirstOrDefaultAsync(
                    x => x.ProductId == sampleRequest.ProductId && x.IsActive,
                    cancellationToken);

            if (product is null)
            {
                return OperationResult<Guid>.Fail("Product does not exist or is inactive.");
            }

            ApplyProductPatch(product, request);

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
        }

        ApplyStatusRules(sampleRequest, patchedProduct, request);

        sampleRequest.UpdatedBy = _currentUser.EmployeeId;
        sampleRequest.UpdatedDate = DateTime.Now;

        if (request.FormulaId is { } selectedFormulaId && selectedFormulaId != Guid.Empty)
        {
            await UpdateSelectedFormulaAsync(
                sampleRequest.ProductId,
                selectedFormulaId,
                cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<Guid>.Ok(
            sampleRequest.SampleRequestId,
            "Updated sample request successfully.");
    }

    private static void ApplySampleRequestPatch(
        SampleRequest sampleRequest,
        PatchSampleRequestCommand request)
    {
        PatchHelper.SetTrimmed(request.Status, () => sampleRequest.Status, value => sampleRequest.Status = value ?? string.Empty);
        PatchHelper.SetTrimmed(request.RequestType, () => sampleRequest.RequestType, value => sampleRequest.RequestType = value ?? string.Empty);
        PatchHelper.SetNullable(request.ExpectedQuantity, () => sampleRequest.ExpectedQuantity, value => sampleRequest.ExpectedQuantity = value);
        PatchHelper.SetNullable(request.ExpectedPrice, () => sampleRequest.ExpectedPrice, value => sampleRequest.ExpectedPrice = value);
        PatchHelper.SetNullable(request.SampleQuantity, () => sampleRequest.SampleQuantity, value => sampleRequest.SampleQuantity = value);
        PatchHelper.SetNullable(request.NumberDeliverySampleDate, () => sampleRequest.NumberDeliverySampleDate, value => sampleRequest.NumberDeliverySampleDate = value);
        PatchHelper.SetTrimmed(request.Package, () => sampleRequest.Package, value => sampleRequest.Package = value ?? string.Empty);
        PatchHelper.SetIfHasValue(request.BagWeight, () => sampleRequest.BagWeight, value => sampleRequest.BagWeight = value);
        PatchHelper.SetTrimmed(request.CustomerProductCode, () => sampleRequest.CustomerProductCode, value => sampleRequest.CustomerProductCode = value);
        PatchHelper.SetNullable(request.RequestDeliveryDate, () => sampleRequest.RequestDeliveryDate, value => sampleRequest.RequestDeliveryDate = value);
        PatchHelper.SetNullable(request.ExpectedDeliveryDate, () => sampleRequest.ExpectedDeliveryDate, value => sampleRequest.ExpectedDeliveryDate = value);
        PatchHelper.SetNullable(request.RealDeliveryDate, () => sampleRequest.RealDeliveryDate, value => sampleRequest.RealDeliveryDate = value);
        PatchHelper.SetNullable(request.RequestTestSampleDate, () => sampleRequest.RequestTestSampleDate, value => sampleRequest.RequestTestSampleDate = value);
        PatchHelper.SetNullable(request.ResponseDeliveryDate, () => sampleRequest.ResponseDeliveryDate, value => sampleRequest.ResponseDeliveryDate = value);
        PatchHelper.SetNullable(request.ExpectedPriceQuoteDate, () => sampleRequest.ExpectedPriceQuoteDate, value => sampleRequest.ExpectedPriceQuoteDate = value);
        PatchHelper.SetNullable(request.RealPriceQuoteDate, () => sampleRequest.RealPriceQuoteDate, value => sampleRequest.RealPriceQuoteDate = value);
        PatchHelper.SetTrimmed(request.InfoType, () => sampleRequest.InfoType, value => sampleRequest.InfoType = value);
        PatchHelper.SetTrimmed(request.OtherComment, () => sampleRequest.OtherComment, value => sampleRequest.OtherComment = value);
        PatchHelper.SetTrimmed(request.SaleComment, () => sampleRequest.SaleComment, value => sampleRequest.SaleComment = value);
        PatchHelper.SetTrimmed(request.AdditionalComment, () => sampleRequest.AdditionalComment, value => sampleRequest.AdditionalComment = value);
    }

    private void ApplyProductPatch(
        Product product,
        PatchSampleRequestCommand request)
    {
        PatchHelper.SetTrimmed(request.ProductName, () => product.Name, value => product.Name = value);
        PatchHelper.SetTrimmed(request.ColourName, () => product.ColourName, value => product.ColourName = value);
        PatchHelper.SetTrimmed(request.Additive, () => product.Additive, value => product.Additive = value);
        PatchHelper.SetNullable(request.UsageRate, () => product.UsageRate, value => product.UsageRate = value);
        PatchHelper.SetTrimmed(request.DeltaE, () => product.DeltaE, value => product.DeltaE = value);
        PatchHelper.SetTrimmed(request.ProductRequirement, () => product.Requirement, value => product.Requirement = value);
        PatchHelper.SetTrimmed(request.ExpiryType, () => product.ExpiryType, value => product.ExpiryType = value);
        PatchHelper.SetNullable(request.StorageCondition, () => product.StorageCondition, value => product.StorageCondition = value);
        PatchHelper.SetTrimmed(request.LabComment, () => product.LabComment, value => product.LabComment = value);
        PatchHelper.SetTrimmed(request.Procedure, () => product.Procedure, value => product.Procedure = value);
        PatchHelper.SetNullable(request.RecycleRate, () => product.RecycleRate, value => product.RecycleRate = value);
        PatchHelper.SetNullable(request.TaicalRate, () => product.TaicalRate, value => product.TaicalRate = value);
        PatchHelper.SetTrimmed(request.Application, () => product.Application, value => product.Application = value);
        PatchHelper.SetTrimmed(request.ProductUsage, () => product.ProductUsage, value => product.ProductUsage = value);
        PatchHelper.SetTrimmed(request.PolymerMatchedIn, () => product.PolymerMatchedIn, value => product.PolymerMatchedIn = value);
        PatchHelper.SetTrimmed(request.ProductCode, () => product.Code, value => product.Code = value);
        PatchHelper.SetTrimmed(request.EndUser, () => product.EndUser, value => product.EndUser = value);
        PatchHelper.SetNullable(request.FoodSafety, () => product.FoodSafety, value => product.FoodSafety = value);
        PatchHelper.SetNullable(request.RohsStandard, () => product.RohsStandard, value => product.RohsStandard = value);
        PatchHelper.SetNullable(request.ReachStandard, () => product.ReachStandard, value => product.ReachStandard = value);
        PatchHelper.SetNullable(request.MaxTemp, () => product.MaxTemp, value => product.MaxTemp = value);
        PatchHelper.SetTrimmed(request.WeatherResistance, () => product.WeatherResistance, value => product.WeatherResistance = value);
        PatchHelper.SetTrimmed(request.LightCondition, () => product.LightCondition, value => product.LightCondition = value);
        PatchHelper.SetTrimmed(request.VisualTest, () => product.VisualTest, value => product.VisualTest = value);
        PatchHelper.SetNullable(request.ReturnSample, () => product.ReturnSample, value => product.ReturnSample = value);
        PatchHelper.SetIfHasValue(request.IsRecycle, () => product.IsRecycle, value => product.IsRecycle = value);
        PatchHelper.SetTrimmed(request.ProductOtherComment, () => product.OtherComment, value => product.OtherComment = value);
        PatchHelper.SetGuidIfValid(request.CategoryId, () => product.CategoryId, value => product.CategoryId = value);
        PatchHelper.SetNullable(request.Weight, () => product.Weight, value => product.Weight = value);
        PatchHelper.SetTrimmed(request.Unit, () => product.Unit, value => product.Unit = value);

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
        CancellationToken cancellationToken)
    {
        var formulas = await _dbContext.Formulas
            .Where(x => x.ProductId == productId && x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var formula in formulas)
        {
            formula.IsSelect = formula.FormulaId == formulaId;
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

    /// <summary>
    /// Xác định có phải cập nhật thông tin product hay không
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    private static bool ShouldPatchProduct(PatchSampleRequestCommand request)
    {
        return request.ColourCode is not null ||
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
}
