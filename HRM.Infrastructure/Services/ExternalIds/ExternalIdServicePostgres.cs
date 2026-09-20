using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Domain.Enums.Category;
using HRM.Infrastructure.DatabaseContext.ApplicationDbs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace HRM.Infrastructure.Services.ExternalIds;

public sealed class ExternalIdServicePostgres : IExternalIdService
{
    private const string GlobalPeriod = "GLOBAL";
    private static readonly Guid SharedCounterCompanyId = Guid.Empty;

    private readonly ApplicationDbContext _context;

    public ExternalIdServicePostgres(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateGlobalCodeAsync(
        Guid companyId,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        _ = companyId;

        if (prefix.Equals(DocumentPrefix.TP.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return await GenerateSampleRequestCodeFromLegacySequenceAsync(prefix, cancellationToken);
        }

        var nextNo = await GetNextNumberAsync(prefix, GlobalPeriod, cancellationToken);

        return $"{prefix}_{nextNo}";
    }

    public async Task<string> GenerateMonthlyCodeAsync(
        Guid companyId,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        _ = companyId;

        var now = DateTime.Now;
        var period = now.ToString("yyMM");

        var nextNo = await GetNextNumberAsync(prefix, period, cancellationToken);

        return $"{prefix}{now:yyMM}{nextNo:00000}";
    }

    private async Task<int> GetNextNumberAsync(
        string prefix,
        string period,
        CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var needOpen = connection.State != System.Data.ConnectionState.Open;

        if (needOpen)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var localTransaction = _context.Database.CurrentTransaction is null
            ? await connection.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction()
                ?? localTransaction;

            await using (var lockCommand = connection.CreateCommand())
            {
                lockCommand.Transaction = transaction;
                lockCommand.CommandText =
                    "SELECT pg_advisory_xact_lock(hashtext(@Prefix), hashtext(@Period));";
                lockCommand.Parameters.Add(new NpgsqlParameter("Prefix", prefix));
                lockCommand.Parameters.Add(new NpgsqlParameter("Period", period));
                await lockCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var counterCommand = connection.CreateCommand();
            counterCommand.Transaction = transaction;
            counterCommand.CommandText = """
            WITH next_number AS
            (
                SELECT COALESCE(MAX("LastNo"), 0) + 1 AS "Value"
                FROM public."IdCounters"
                WHERE "Prefix" = @Prefix
                  AND "Period" = @Period
            )
            INSERT INTO public."IdCounters" ("CompanyId", "Prefix", "Period", "LastNo")
            SELECT @SharedCompanyId, @Prefix, @Period, "Value"
            FROM next_number
            ON CONFLICT ("CompanyId", "Prefix", "Period")
            DO UPDATE SET "LastNo" = GREATEST(
                public."IdCounters"."LastNo",
                EXCLUDED."LastNo")
            RETURNING "LastNo";
            """;

            counterCommand.Parameters.Add(new NpgsqlParameter("Prefix", prefix));
            counterCommand.Parameters.Add(new NpgsqlParameter("Period", period));
            counterCommand.Parameters.Add(new NpgsqlParameter("SharedCompanyId", SharedCounterCompanyId));

            var result = await counterCommand.ExecuteScalarAsync(cancellationToken);

            if (localTransaction is not null)
            {
                await localTransaction.CommitAsync(cancellationToken);
            }

            return Convert.ToInt32(result);
        }
        finally
        {
            if (needOpen)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<string> GenerateSampleRequestCodeFromLegacySequenceAsync(
        string prefix,
        CancellationToken cancellationToken)
    {
        var basePrefix = $"{prefix}_";

        var lastCode = await _context.SampleRequests
            .AsNoTracking()
            .Where(x => x.ExternalId.StartsWith(basePrefix))
            .OrderByDescending(x => x.ExternalId.Length)
            .ThenByDescending(x => x.ExternalId)
            .Select(x => x.ExternalId)
            .FirstOrDefaultAsync(cancellationToken);

        var nextNumber = 1;

        if (!string.IsNullOrWhiteSpace(lastCode) &&
            lastCode.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var numberPart = lastCode[basePrefix.Length..];
            if (int.TryParse(numberPart, out var lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        return $"{basePrefix}{nextNumber}";
    }

}
