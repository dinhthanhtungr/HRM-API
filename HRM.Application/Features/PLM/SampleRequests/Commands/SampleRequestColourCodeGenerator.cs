using System.Text.RegularExpressions;
using HRM.Application.Commons.Models;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Domain.ReferenceData.SampleRequests;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands;

internal static class SampleRequestColourCodeGenerator
{
    public static async Task<OperationResult<SampleRequestColourCodeResult>> ResolveAsync(
        IPLMWriteDbContext dbContext,
        string? value,
        Guid? excludedProductId,
        string? currentColourCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return OperationResult<SampleRequestColourCodeResult>.Ok(
                new SampleRequestColourCodeResult(null, null));
        }

        var normalized = value.Trim().ToUpperInvariant();

        if (!normalized.Contains('_'))
        {
            return OperationResult<SampleRequestColourCodeResult>.Ok(
                new SampleRequestColourCodeResult(normalized, null));
        }

        var templateParts = normalized.Split('_', 2, StringSplitOptions.TrimEntries);
        var prefix = templateParts[0];
        var suffix = templateParts.Length > 1 ? templateParts[1] : string.Empty;

        if (string.IsNullOrWhiteSpace(prefix))
        {
            return OperationResult<SampleRequestColourCodeResult>.Ok(
                new SampleRequestColourCodeResult(normalized.Replace("_", string.Empty), null));
        }

        var additiveCode = ResolveAdditiveCode(suffix);
        if (!string.IsNullOrWhiteSpace(suffix) && additiveCode is null)
        {
            return OperationResult<SampleRequestColourCodeResult>.Fail("ColourCode additive suffix is invalid.");
        }

        var existingCodes = await dbContext.Products
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.ColourCode != null &&
                x.ColourCode.StartsWith(prefix) &&
                (!excludedProductId.HasValue || x.ProductId != excludedProductId.Value))
            .Select(x => x.ColourCode!)
            .ToListAsync(cancellationToken);

        var nextNumber = ResolveStartNumber(currentColourCode, prefix, existingCodes);
        var existingCodeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string candidate;

        do
        {
            candidate = $"{prefix}{nextNumber:000}{suffix}";
            nextNumber++;
        }
        while (existingCodeSet.Contains(candidate));

        return OperationResult<SampleRequestColourCodeResult>.Ok(
            new SampleRequestColourCodeResult(candidate, additiveCode));
    }

    private static int ResolveStartNumber(
        string? currentColourCode,
        string prefix,
        IReadOnlyList<string> existingCodes)
    {
        var currentNumber = ExtractRunningNumber(currentColourCode, prefix);
        if (currentNumber.HasValue)
        {
            return currentNumber.Value;
        }

        return existingCodes
            .Select(code => ExtractRunningNumber(code, prefix))
            .Where(number => number.HasValue)
            .Select(number => number!.Value)
            .DefaultIfEmpty(0)
            .Max() + 1;
    }

    private static string? ResolveAdditiveCode(string suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return null;
        }

        var normalized = suffix.Trim().ToUpperInvariant();

        var exactMatch = SampleRequestReferenceData.FindAdditive(normalized);
        if (exactMatch is not null)
        {
            return exactMatch.Code;
        }

        if (normalized.Length == 1)
        {
            return SampleRequestReferenceData.Additives
                .FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x.Code) &&
                    string.Equals(x.GroupCode, normalized, StringComparison.OrdinalIgnoreCase))
                ?.Code;
        }

        return null;
    }

    private static int? ExtractRunningNumber(string? colourCode, string prefix)
    {
        if (string.IsNullOrWhiteSpace(colourCode))
        {
            return null;
        }

        if (!colourCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var suffix = colourCode[prefix.Length..];
        var match = Regex.Match(suffix, @"^(\d+)");

        return match.Success && int.TryParse(match.Groups[1].Value, out var number)
            ? number
            : null;
    }
}

internal sealed record SampleRequestColourCodeResult(
    string? ColourCode,
    string? AdditiveCode);
