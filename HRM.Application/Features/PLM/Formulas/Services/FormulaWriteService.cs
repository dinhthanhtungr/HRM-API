using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Features.PLM.Formulas.Commands.UpdateFormulaStatus;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.Category;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Products;
using HRM.Domain.Enums.SampleRequests;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Formulas.Services;

internal sealed class FormulaWriteService
{
    private const decimal MaxQuantity = 999999999999.9999999999m;
    private const decimal MaxPrice = 9999999999999999.999999m;

    private readonly IPLMWriteDbContext _dbContext;
    private readonly IExternalIdService _externalIdService;

    public FormulaWriteService(
        IPLMWriteDbContext dbContext,
        IExternalIdService externalIdService)
    {
        _dbContext = dbContext;
        _externalIdService = externalIdService;
    }

    public async Task<string> ResolveExternalIdAsync(
        Guid companyId,
        string? requestedExternalId,
        Guid? excludedFormulaId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestedExternalId))
        {
            return await _externalIdService.GenerateMonthlyCodeAsync(
                companyId,
                DocumentPrefix.VU.ToString(),
                cancellationToken);
        }

        var externalId = requestedExternalId.Trim();
        if (externalId.Length > 50)
        {
            throw new InvalidOperationException("Formula external id must not exceed 50 characters.");
        }

        var duplicated = await _dbContext.Formulas
            .AsNoTracking()
            .AnyAsync(x =>
                x.CompanyId == companyId &&
                x.ExternalId == externalId &&
                (!excludedFormulaId.HasValue || x.FormulaId != excludedFormulaId.Value),
                cancellationToken);

        if (duplicated)
        {
            throw new InvalidOperationException("Formula external id already exists.");
        }

        return externalId;
    }

    public static string NormalizeRequiredName(string? name)
    {
        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Formula name is required.");
        }

        if (normalizedName.Length > 200)
        {
            throw new InvalidOperationException("Formula name must not exceed 200 characters.");
        }

        return normalizedName;
    }

    public async Task<string> ResolveNameAsync(
        Guid companyId,
        Guid productId,
        string? requestedName,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedName))
        {
            return NormalizeRequiredName(requestedName);
        }

        var existingNames = await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.ProductId == productId &&
                x.IsActive)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

        var nextNumber = existingNames
            .Select(TryParseGeneratedNameNumber)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"F{nextNumber:000}";
    }

    public async Task<Product> LoadProductAsync(
        Guid companyId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (productId == Guid.Empty)
        {
            throw new InvalidOperationException("ProductId is required.");
        }

        var product = await _dbContext.Products
            .FirstOrDefaultAsync(x =>
                x.ProductId == productId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);

        return product ?? throw new InvalidOperationException("Product was not found or is inactive.");
    }

    public async Task ReplaceMaterialsAsync(
        Formula formula,
        IReadOnlyList<UpsertFormulaMaterialRequest>? requests,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (requests is null)
        {
            return;
        }

        var activeRows = await _dbContext.FormulaMaterials
            .Where(x => x.FormulaId == formula.FormulaId && x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var row in activeRows)
        {
            row.IsActive = false;
        }

        var lineNo = 1;
        foreach (var request in requests)
        {
            var row = await BuildMaterialRowAsync(
                formula,
                request,
                companyId,
                lineNo,
                cancellationToken);

            await _dbContext.FormulaMaterials.AddAsync(row, cancellationToken);
            lineNo++;
        }

        formula.TotalPrice = requests.Sum(x => RoundPrice(x.Quantity * (x.UnitPrice ?? 0m)));
    }

    public async Task<IReadOnlyList<SampleRequestSampleSentTarget>> MarkSampleRequestsAsSampleSentAsync(
        Guid formulaId,
        Guid formulaProductId,
        Guid companyId,
        Guid employeeId,
        DateTime now,
        Guid sentByEmployeeId,
        DateTime sentDate,
        Guid? sampleRequestId,
        CancellationToken cancellationToken)
    {
        var sampleRequests = sampleRequestId.HasValue
            ? await LoadTargetSampleRequestsAsync(
                formulaId,
                formulaProductId,
                companyId,
                sampleRequestId.Value,
                cancellationToken)
            : await _dbContext.SampleRequests
                .Where(x =>
                    x.FormulaId == formulaId &&
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    x.Status != SampleRequestStatus.SampleSent.ToString())
                .ToListAsync(cancellationToken);

        foreach (var sampleRequest in sampleRequests)
        {
            sampleRequest.Status = SampleRequestStatus.SampleSent.ToString();
            sampleRequest.SendBy = sentByEmployeeId;
            sampleRequest.SendDate = sentDate;
            sampleRequest.UpdatedBy = employeeId;
            sampleRequest.UpdatedDate = now;
        }

        return sampleRequests
            .Select(x => new SampleRequestSampleSentTarget(x.SampleRequestId, x.ExternalId))
            .ToArray();
    }

    public async Task<SampleRequestSampleTrial> EnsureSampleSentTrialAsync(
        SampleRequest sampleRequest,
        Guid formulaId,
        string formulaExternalId,
        Guid companyId,
        Guid currentEmployeeId,
        DateTime now,
        decimal deliveredSampleQuantityKg,
        CancellationToken cancellationToken)
    {
        var trial = await _dbContext.SampleRequestSampleTrials
            .Where(x =>
                x.SampleRequestId == sampleRequest.SampleRequestId &&
                x.IsActive &&
                x.Status == SampleTrialStatus.Draft &&
                (!x.FormulaId.HasValue || x.FormulaId == formulaId))
            .OrderByDescending(x => x.TrialNo)
            .FirstOrDefaultAsync(cancellationToken);

        if (trial is null)
        {
            var nextTrialNo = (await _dbContext.SampleRequestSampleTrials
                .Where(x => x.SampleRequestId == sampleRequest.SampleRequestId)
                .Select(x => (int?)x.TrialNo)
                .MaxAsync(cancellationToken) ?? 0) + 1;

            trial = new SampleRequestSampleTrial
            {
                SampleRequestSampleTrialId = Guid.CreateVersion7(),
                SampleRequestId = sampleRequest.SampleRequestId,
                TrialNo = nextTrialNo,
                CreatedBy = currentEmployeeId,
                CreatedDate = now,
                IsActive = true
            };

            await _dbContext.SampleRequestSampleTrials.AddAsync(trial, cancellationToken);
        }

        FormulaSampleSentRules.PrepareTrialForDelivery(
            trial,
            formulaId,
            formulaExternalId,
            currentEmployeeId,
            now,
            deliveredSampleQuantityKg);

        SampleRequestSampleTrialMutationRules.PopulateSnapshots(trial, sampleRequest);
        return trial;
    }

    public async Task<IReadOnlyList<SampleRequestFormulaCompletedTarget>> MarkSampleRequestsAsFormulaCompletedAsync(
        Guid formulaId,
        Guid formulaProductId,
        Guid companyId,
        Guid employeeId,
        DateTime now,
        Guid? sampleRequestId,
        CancellationToken cancellationToken)
    {
        var sampleRequests = sampleRequestId.HasValue
            ? await LoadTargetSampleRequestsAsync(
                formulaId,
                formulaProductId,
                companyId,
                sampleRequestId.Value,
                cancellationToken)
            : await _dbContext.SampleRequests
                .Where(x =>
                    x.FormulaId == formulaId &&
                    x.CompanyId == companyId &&
                    x.IsActive &&
                    x.Status != SampleRequestStatus.Completed.ToString())
                .ToListAsync(cancellationToken);

        foreach (var sampleRequest in sampleRequests)
        {
            sampleRequest.FormulaId = formulaId;
            sampleRequest.Status = SampleRequestStatus.Completed.ToString();
            sampleRequest.UpdatedBy = employeeId;
            sampleRequest.UpdatedDate = now;
        }

        var productFormulas = await _dbContext.Formulas
            .Where(x =>
                x.CompanyId == companyId &&
                x.ProductId == formulaProductId &&
                x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var productFormula in productFormulas)
        {
            productFormula.IsSelect = productFormula.FormulaId == formulaId;
            if (productFormula.FormulaId == formulaId)
            {
                productFormula.Status = FormulaStatus.Completed.ToString();
                productFormula.UpdatedBy = employeeId;
                productFormula.UpdatedDate = now;
            }
        }

        return sampleRequests
            .Select(x => new SampleRequestFormulaCompletedTarget(x.SampleRequestId, x.ExternalId))
            .ToArray();
    }

    private async Task<List<SampleRequest>> LoadTargetSampleRequestsAsync(
        Guid formulaId,
        Guid formulaProductId,
        Guid companyId,
        Guid sampleRequestId,
        CancellationToken cancellationToken)
    {
        if (sampleRequestId == Guid.Empty)
        {
            throw new InvalidOperationException("SampleRequestId is invalid.");
        }

        var sampleRequest = await _dbContext.SampleRequests
            .Include(x => x.Customer)
            .Include(x => x.Product)
            .ThenInclude(x => x.Category)
            .FirstOrDefaultAsync(x =>
                x.SampleRequestId == sampleRequestId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);

        if (sampleRequest is null)
        {
            throw new InvalidOperationException("Sample request was not found or is inactive.");
        }

        if (sampleRequest.ProductId != formulaProductId)
        {
            throw new InvalidOperationException("Formula does not belong to this sample request product.");
        }

        if (string.Equals(sampleRequest.Status, SampleRequestStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sampleRequest.Status, SampleRequestStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sampleRequest.Status, SampleRequestStatus.FormulaUpdateRequested.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This sample request cannot receive a new sample in its current status.");
        }

        if (sampleRequest.FormulaId.HasValue &&
            sampleRequest.FormulaId.Value != Guid.Empty &&
            sampleRequest.FormulaId.Value != formulaId &&
            string.Equals(sampleRequest.Status, SampleRequestStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Completed sample request cannot be relinked through formula status update.");
        }

        return [sampleRequest];
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<FormulaMaterial> BuildMaterialRowAsync(
        Formula formula,
        UpsertFormulaMaterialRequest request,
        Guid companyId,
        int fallbackLineNo,
        CancellationToken cancellationToken)
    {
        ValidateMaterialRequest(request);

        var lineNo = request.LineNo > 0 ? request.LineNo : fallbackLineNo;
        var unitPrice = RoundPrice(request.UnitPrice ?? 0m);
        var quantity = RoundQuantity(request.Quantity);
        var totalPrice = RoundPrice(quantity * unitPrice);

        var materialId = IsMaterialType(request.ItemType) ? request.ItemId : (Guid?)null;
        var productId = IsProductType(request.ItemType) ? request.ItemId : (Guid?)null;

        var snapshot = await ResolveItemSnapshotAsync(
            request,
            companyId,
            cancellationToken);

        return new FormulaMaterial
        {
            FormulaMaterialId = Guid.CreateVersion7(),
            FormulaId = formula.FormulaId,
            MaterialId = materialId,
            ProductId = productId,
            CategoryId = request.CategoryId ?? snapshot.CategoryId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalPrice = totalPrice,
            itemType = request.ItemType,
            MaterialNameSnapshot = string.IsNullOrWhiteSpace(request.MaterialNameSnapshot)
                ? snapshot.Name
                : request.MaterialNameSnapshot.Trim(),
            MaterialExternalIdSnapshot = string.IsNullOrWhiteSpace(request.MaterialExternalIdSnapshot)
                ? snapshot.ExternalId
                : request.MaterialExternalIdSnapshot.Trim(),
            Unit = string.IsNullOrWhiteSpace(request.Unit)
                ? snapshot.Unit
                : request.Unit.Trim(),
            IsActive = true,
            LineNo = lineNo,
            Formula = formula
        };
    }

    private static void ValidateMaterialRequest(UpsertFormulaMaterialRequest request)
    {
        if (request.ItemId == Guid.Empty)
        {
            throw new InvalidOperationException("Formula material item id is required.");
        }

        if (!Enum.IsDefined(typeof(ItemType), request.ItemType))
        {
            throw new InvalidOperationException("Formula material item type is invalid.");
        }

        if (request.Quantity <= 0 || request.Quantity > MaxQuantity)
        {
            throw new InvalidOperationException("Formula material quantity is invalid.");
        }

        if (request.UnitPrice is < 0 or > MaxPrice)
        {
            throw new InvalidOperationException("Formula material unit price is invalid.");
        }
    }

    private async Task<ItemSnapshot> ResolveItemSnapshotAsync(
        UpsertFormulaMaterialRequest request,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (IsMaterialType(request.ItemType))
        {
            var material = await _dbContext.Materials
                .AsNoTracking()
                .Where(x =>
                    x.MaterialId == request.ItemId &&
                    x.CompanyId == companyId &&
                    x.IsActive == true)
                .Select(x => new ItemSnapshot(
                    x.CategoryId,
                    x.Name ?? string.Empty,
                    x.ExternalId ?? string.Empty,
                    x.Unit))
                .FirstOrDefaultAsync(cancellationToken);

            return material ?? throw new InvalidOperationException("Formula material was not found or is inactive.");
        }

        var product = await _dbContext.Products
            .AsNoTracking()
            .Where(x =>
                x.ProductId == request.ItemId &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new ItemSnapshot(
                x.CategoryId,
                x.Name ?? string.Empty,
                x.ColourCode ?? x.Code ?? string.Empty,
                x.Unit))
            .FirstOrDefaultAsync(cancellationToken);

        return product ?? throw new InvalidOperationException("Formula product item was not found or is inactive.");
    }

    private static bool IsMaterialType(ItemType itemType)
    {
        return itemType is ItemType.Material or ItemType.MaterialFailure;
    }

    private static bool IsProductType(ItemType itemType)
    {
        return itemType is ItemType.Product or ItemType.ProductFailure;
    }

    private static decimal RoundQuantity(decimal value)
    {
        return Math.Round(value, 10, MidpointRounding.AwayFromZero);
    }

    private static decimal RoundPrice(decimal value)
    {
        return Math.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private static int? TryParseGeneratedNameNumber(string? name)
    {
        var normalizedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) ||
            normalizedName.Length < 2 ||
            normalizedName[0] != 'F')
        {
            return null;
        }

        var numberText = normalizedName[1..];
        return numberText.All(char.IsDigit) &&
            int.TryParse(numberText, out var number)
            ? number
            : null;
    }

    public static FormulaWriteResultDto ToResult(
        Formula formula,
        int updatedSampleRequestCount = 0,
        Guid? sampleRequestSampleTrialId = null)
    {
        return new FormulaWriteResultDto
        {
            FormulaId = formula.FormulaId,
            SampleRequestSampleTrialId = sampleRequestSampleTrialId,
            ExternalId = formula.ExternalId,
            Status = formula.Status,
            UpdatedSampleRequestCount = updatedSampleRequestCount,
            UpdatedDate = formula.UpdatedDate
        };
    }

    public sealed record SampleRequestSampleSentTarget(
        Guid SampleRequestId,
        string ExternalId);

    public sealed record SampleRequestFormulaCompletedTarget(
        Guid SampleRequestId,
        string ExternalId);

    private sealed record ItemSnapshot(
        Guid CategoryId,
        string Name,
        string ExternalId,
        string? Unit);
}
