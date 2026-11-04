using HRM.Application.Features.PLM.Formulas.Services;

namespace HRM.Application.Tests.Features.PLM.Formulas;

public sealed class FormulaTimestampPrecisionTests
{
    [Fact]
    public void WriteResponseCanBeUsedAfterPostgresTruncatesSubMicrosecondTicks()
    {
        var responseTimestamp = new DateTime(2026, 10, 31, 13, 57, 19).AddTicks(3901882);
        var storedTimestamp = FormulaConcurrencyRules.NormalizeDatabaseTimestamp(responseTimestamp);

        Assert.False(FormulaConcurrencyRules.HasExpectedUpdatedDateConflictWithDatabasePrecision(
            responseTimestamp, storedTimestamp));
        Assert.True(FormulaConcurrencyRules.HasExpectedUpdatedDateConflictWithDatabasePrecision(
            responseTimestamp, storedTimestamp.AddMicroseconds(1)));
    }
}
