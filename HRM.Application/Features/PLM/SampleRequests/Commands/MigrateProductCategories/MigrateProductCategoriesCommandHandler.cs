using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Entities.SampleRequestSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.MigrateProductCategories;

internal sealed class MigrateProductCategoriesCommandHandler
    : IRequestHandler<MigrateProductCategoriesCommand, OperationResult<ProductCategoryMigrationResult>>
{
    private const int PreviewItemLimit = 100;
    private const string LegacyCompoundCategoryName = "Compound";
    private const string LegacyColorMasterbatchCategoryName = "Hạt màu";
    private const string NormalizedPigmentProductNameKeyword = "BOT MAU";
    private static readonly string[] NormalizedColorMasterbatchProductNameKeywords =
    [
        "HAT MAU",
        "HAT NHUA MAU"
    ];

    private static readonly string[] ExcludedColorMasterbatchNameKeywords =
    [
        "HAT PHU GIA",
        "COMPOUND",
        "BOT MAU",
        "BOT PHU GIA"
    ];

    private static readonly string[] ExcludedPigmentNameKeywords =
    [
        "BOT PHU GIA",
        "PHU GIA",
        "HAT NHUA PHU GIA"
    ];

    private static readonly string[] NormalizedAdditiveMasterbatchProductNameKeywords =
    [
        "HAT PHU GIA",
        "HAT NHUA PHU GIA"
    ];

    private static readonly string[] ExcludedAdditiveMasterbatchSuffixNameKeywords =
    [
        "MAU",
        "BOT PHU GIA"
    ];

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public MigrateProductCategoriesCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<ProductCategoryMigrationResult>> Handle(
        MigrateProductCategoriesCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInAnyRole(ApplicationRoleSets.PLM.ProductTechnicalEditors))
        {
            return OperationResult<ProductCategoryMigrationResult>.Fail(
                "You are not allowed to migrate product categories.");
        }

        var targetCategoryCode = NormalizeTargetCategoryCode(request.TargetCategoryCode);
        if (request.TargetCategoryCode is not null && targetCategoryCode is null)
        {
            return OperationResult<ProductCategoryMigrationResult>.Fail(
                "TargetCategoryCode must be CMP, CMB, AMB, ADD, or PIG.");
        }

        var employeeId = _currentUser.EmployeeId.GetValueOrDefault();
        if (employeeId == Guid.Empty)
        {
            return OperationResult<ProductCategoryMigrationResult>.Fail("Current employee is invalid.");
        }

        var targetCategories = await _dbContext.Categories
            .AsNoTracking()
            .Where(x =>
                x.IsActive == true &&
                x.Types == "Product" &&
                (x.ExternalId == SampleRequestProductCategoryRules.CompoundCode ||
                 x.ExternalId == SampleRequestProductCategoryRules.ColorMasterbatchCode ||
                 x.ExternalId == SampleRequestProductCategoryRules.AdditiveMasterbatchCode ||
                 x.ExternalId == SampleRequestProductCategoryRules.AdditiveCode ||
                 x.ExternalId == SampleRequestProductCategoryRules.PigmentCode))
            .Select(x => new TargetCategory(x.CompanyId, x.CategoryId, x.ExternalId!))
            .ToListAsync(cancellationToken);

        var products = await _dbContext.Products
            .Include(x => x.Category)
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);

        var canonicalCategoryIdsByCompany = targetCategories
            .GroupBy(x => x.CompanyId)
            .Select(group => new
            {
                CompanyId = group.Key,
                CompoundCategoryId = ResolveSingleCategoryId(group, SampleRequestProductCategoryRules.CompoundCode),
                ColorMasterbatchCategoryId = ResolveSingleCategoryId(group, SampleRequestProductCategoryRules.ColorMasterbatchCode),
                AdditiveMasterbatchCategoryId = ResolveSingleCategoryId(group, SampleRequestProductCategoryRules.AdditiveMasterbatchCode),
                AdditiveCategoryId = ResolveSingleCategoryId(group, SampleRequestProductCategoryRules.AdditiveCode),
                PigmentCategoryId = ResolveSingleCategoryId(group, SampleRequestProductCategoryRules.PigmentCode)
            })
            .Where(x =>
                x.CompoundCategoryId is not null &&
                x.ColorMasterbatchCategoryId is not null &&
                x.AdditiveMasterbatchCategoryId is not null &&
                x.AdditiveCategoryId is not null &&
                x.PigmentCategoryId is not null)
            .ToDictionary(
                x => x.CompanyId,
                x => new CanonicalCategoryIds(
                    x.CompoundCategoryId!.Value,
                    x.ColorMasterbatchCategoryId!.Value,
                    x.AdditiveMasterbatchCategoryId!.Value,
                    x.AdditiveCategoryId!.Value,
                    x.PigmentCategoryId!.Value));

        if (products.Any(x => !canonicalCategoryIdsByCompany.ContainsKey(x.CompanyId)))
        {
            return OperationResult<ProductCategoryMigrationResult>.Fail(
                "Canonical CMP, CMB, AMB, ADD and PIG categories must each exist once and be active for every company with active products.");
        }

        var compoundToCmpCount = 0;
        var colorMasterbatchToCmbCount = 0;
        var additiveMasterbatchToAmbCount = 0;
        var additiveToAddCount = 0;
        var pigmentToPigCount = 0;
        var candidates = new List<MigrationCandidate>();
        var now = _dateTimeProvider.Now;

        foreach (var product in products)
        {
            var match = ResolveMatch(product);
            if (match is null)
            {
                continue;
            }

            if (targetCategoryCode is not null && match.TargetCategoryCode != targetCategoryCode)
            {
                continue;
            }

            var categoryIds = canonicalCategoryIdsByCompany[product.CompanyId];
            var targetCategoryId = match.TargetCategoryCode switch
            {
                SampleRequestProductCategoryRules.CompoundCode => categoryIds.CompoundCategoryId,
                SampleRequestProductCategoryRules.ColorMasterbatchCode => categoryIds.ColorMasterbatchCategoryId,
                SampleRequestProductCategoryRules.AdditiveMasterbatchCode => categoryIds.AdditiveMasterbatchCategoryId,
                SampleRequestProductCategoryRules.AdditiveCode => categoryIds.AdditiveCategoryId,
                _ => categoryIds.PigmentCategoryId
            };
            if (product.CategoryId == targetCategoryId)
            {
                continue;
            }

            candidates.Add(new MigrationCandidate(product, targetCategoryId, match));

            if (match.TargetCategoryCode == SampleRequestProductCategoryRules.CompoundCode)
            {
                compoundToCmpCount++;
            }
            else if (match.TargetCategoryCode == SampleRequestProductCategoryRules.ColorMasterbatchCode)
            {
                colorMasterbatchToCmbCount++;
            }
            else if (match.TargetCategoryCode == SampleRequestProductCategoryRules.AdditiveMasterbatchCode)
            {
                additiveMasterbatchToAmbCount++;
            }
            else if (match.TargetCategoryCode == SampleRequestProductCategoryRules.AdditiveCode)
            {
                additiveToAddCount++;
            }
            else
            {
                pigmentToPigCount++;
            }
        }

        if (!request.DryRun)
        {
            foreach (var candidate in candidates)
            {
                candidate.Product.CategoryId = candidate.TargetCategoryId;
                candidate.Product.UpdatedBy = employeeId;
                candidate.Product.UpdatedDate = now;
            }

            if (candidates.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return OperationResult<ProductCategoryMigrationResult>.Ok(
            new ProductCategoryMigrationResult
            {
                DryRun = request.DryRun,
                TargetCategoryCode = targetCategoryCode,
                CompoundToCmpCount = compoundToCmpCount,
                ColorMasterbatchToCmbCount = colorMasterbatchToCmbCount,
                AdditiveMasterbatchToAmbCount = additiveMasterbatchToAmbCount,
                AdditiveToAddCount = additiveToAddCount,
                PigmentToPigCount = pigmentToPigCount,
                PreviewItems = candidates
                    .OrderBy(x => x.Product.ProductId)
                    .Take(PreviewItemLimit)
                    .Select(x => new ProductCategoryMigrationPreviewItem
                    {
                        ProductId = x.Product.ProductId,
                        ColourCode = x.Product.ColourCode,
                        ProductName = x.Product.Name,
                        CurrentCategoryName = x.Product.Category?.Name,
                        TargetCategoryCode = x.Match.TargetCategoryCode,
                        MatchedBy = x.Match.MatchedBy
                    })
                    .ToList(),
                HasMorePreviewItems = candidates.Count > PreviewItemLimit
            },
            request.DryRun
                ? "Product category migration preview completed. No database changes were made."
                : "Product category migration completed.");
    }

    private static Guid? ResolveSingleCategoryId(
        IEnumerable<TargetCategory> categories,
        string externalId)
    {
        var matches = categories
            .Where(x => string.Equals(x.ExternalId, externalId, StringComparison.Ordinal))
            .Select(x => x.CategoryId)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    private static string? NormalizeTargetCategoryCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim().ToUpperInvariant();
        return normalizedValue is SampleRequestProductCategoryRules.CompoundCode or
            SampleRequestProductCategoryRules.ColorMasterbatchCode or
            SampleRequestProductCategoryRules.AdditiveMasterbatchCode or
            SampleRequestProductCategoryRules.AdditiveCode or
            SampleRequestProductCategoryRules.PigmentCode
            ? normalizedValue
            : null;
    }

    private static ProductCategoryMigrationMatch? ResolveMatch(Product product)
    {
        if (HasExactText(product.Category?.Name, LegacyCompoundCategoryName))
        {
            return new ProductCategoryMigrationMatch(
                SampleRequestProductCategoryRules.CompoundCode,
                "legacy-category-compound");
        }

        if (EndsWithCode(product.ColourCode, 'C'))
        {
            return new ProductCategoryMigrationMatch(
                SampleRequestProductCategoryRules.CompoundCode,
                "colour-code-suffix-c");
        }

        var normalizedProductName = NormalizeForKeywordSearch(product.Name);
        var isExcludedFromPigment = ExcludedPigmentNameKeywords
            .Any(keyword => ContainsNormalizedPhrase(normalizedProductName, keyword));
        var hasPigmentName = ContainsNormalizedPhrase(
            normalizedProductName,
            NormalizedPigmentProductNameKeyword);
        if (hasPigmentName || (!isExcludedFromPigment && EndsWithCode(product.ColourCode, 'D')))
        {
            return new ProductCategoryMigrationMatch(
                SampleRequestProductCategoryRules.PigmentCode,
                hasPigmentName
                    ? "normalized-product-name-pigment"
                    : "colour-code-suffix-d-pigment");
        }

        var isExcludedFromColorMasterbatch = ExcludedColorMasterbatchNameKeywords
            .Any(keyword => ContainsNormalizedPhrase(normalizedProductName, keyword));
        var hasColorMasterbatchName = NormalizedColorMasterbatchProductNameKeywords
            .Any(keyword => ContainsNormalizedPhrase(normalizedProductName, keyword));
        if (!isExcludedFromColorMasterbatch &&
            (HasExactText(product.Category?.Name, LegacyColorMasterbatchCategoryName) ||
             (hasColorMasterbatchName &&
              HasAllowedColorMasterbatchColourCodeSuffix(product.ColourCode))))
        {
            return new ProductCategoryMigrationMatch(
                SampleRequestProductCategoryRules.ColorMasterbatchCode,
                HasExactText(product.Category?.Name, LegacyColorMasterbatchCategoryName)
                    ? "legacy-category-color-masterbatch"
                    : "normalized-product-name-color-masterbatch-allowed-suffix");
        }

        var hasAdditiveMasterbatchName = NormalizedAdditiveMasterbatchProductNameKeywords
            .Any(keyword => ContainsNormalizedPhrase(normalizedProductName, keyword));
        if (hasAdditiveMasterbatchName)
        {
            return new ProductCategoryMigrationMatch(
                SampleRequestProductCategoryRules.AdditiveMasterbatchCode,
                "normalized-product-name-additive-masterbatch");
        }

        var hasPowderAdditiveName = ContainsNormalizedPhrase(normalizedProductName, "BOT PHU GIA");
        var hasAdditiveName = ContainsNormalizedPhrase(normalizedProductName, "PHU GIA");
        if (hasPowderAdditiveName || (hasAdditiveName && EndsWithCode(product.ColourCode, 'D')))
        {
            return new ProductCategoryMigrationMatch(
                SampleRequestProductCategoryRules.AdditiveCode,
                hasPowderAdditiveName
                    ? "normalized-product-name-powder-additive"
                    : "colour-code-suffix-d-additive");
        }

        var isExcludedFromAdditiveMasterbatchSuffix = ExcludedAdditiveMasterbatchSuffixNameKeywords
            .Any(keyword => ContainsNormalizedPhrase(normalizedProductName, keyword));
        if (!isExcludedFromAdditiveMasterbatchSuffix && EndsWithLetterOtherThan(product.ColourCode, 'C'))
        {
            return new ProductCategoryMigrationMatch(
                SampleRequestProductCategoryRules.AdditiveMasterbatchCode,
                "colour-code-letter-suffix-additive-masterbatch");
        }

        return null;
    }

    private static bool HasExactText(string? value, string expected)
        => string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsNormalizedPhrase(string? value, string normalizedPhrase)
    {
        var normalizedValue = NormalizeForKeywordSearch(value);
        return $" {normalizedValue} ".Contains($" {normalizedPhrase} ", StringComparison.Ordinal);
    }

    private static string NormalizeForKeywordSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value
            .Replace('Đ', 'D')
            .Replace('đ', 'd')
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character)
                ? char.ToUpperInvariant(character)
                : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool EndsWithCode(string? value, char suffix)
        => value?.Trim().EndsWith(suffix.ToString(), StringComparison.OrdinalIgnoreCase) == true;

    private static bool EndsWithLetterOtherThan(string? value, char excludedSuffix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var suffix = value.Trim()[^1];
        return char.IsLetter(suffix) && char.ToUpperInvariant(suffix) != char.ToUpperInvariant(excludedSuffix);
    }

    private static bool HasAllowedColorMasterbatchColourCodeSuffix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var suffix = value.Trim()[^1];
        return !char.IsLetter(suffix) || suffix is 'U' or 'u' or 'A' or 'a';
    }

    private sealed record TargetCategory(Guid CompanyId, Guid CategoryId, string ExternalId);
    private sealed record CanonicalCategoryIds(
        Guid CompoundCategoryId,
        Guid ColorMasterbatchCategoryId,
        Guid AdditiveMasterbatchCategoryId,
        Guid AdditiveCategoryId,
        Guid PigmentCategoryId);
    private sealed record ProductCategoryMigrationMatch(string TargetCategoryCode, string MatchedBy);
    private sealed record MigrationCandidate(
        Product Product,
        Guid TargetCategoryId,
        ProductCategoryMigrationMatch Match);
}
