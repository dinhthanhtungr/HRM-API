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
        if (prefix.Equals(DocumentPrefix.TP.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return await GenerateSampleRequestCodeFromLegacySequenceAsync(prefix, cancellationToken);
        }

        var nextNo = await GetNextNumberAsync(companyId, prefix, GlobalPeriod, cancellationToken);

        return $"{prefix}_{nextNo}";
    }

    public async Task<string> GenerateMonthlyCodeAsync(
        Guid companyId,
        string prefix,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var period = now.ToString("yyMM");

        var nextNo = await GetNextNumberAsync(companyId, prefix, period, cancellationToken);

        return $"{prefix}{now:yyMM}{nextNo:00000}";
    }

    private async Task<int> GetNextNumberAsync(
        Guid companyId,
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

        try
        {
            await using var command = connection.CreateCommand();

            if (_context.Database.CurrentTransaction is IDbContextTransaction transaction)
            {
                command.Transaction = transaction.GetDbTransaction();
            }

            command.CommandText = """
            INSERT INTO public."IdCounters" ("CompanyId", "Prefix", "Period", "LastNo")
            VALUES (@CompanyId, @Prefix, @Period, 1)
            ON CONFLICT ("CompanyId", "Prefix", "Period")
            DO UPDATE SET "LastNo" = public."IdCounters"."LastNo" + 1
            RETURNING "LastNo";
            """;

            command.Parameters.Add(new NpgsqlParameter("CompanyId", companyId));
            command.Parameters.Add(new NpgsqlParameter("Prefix", prefix));
            command.Parameters.Add(new NpgsqlParameter("Period", period));

            var result = await command.ExecuteScalarAsync(cancellationToken);

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
