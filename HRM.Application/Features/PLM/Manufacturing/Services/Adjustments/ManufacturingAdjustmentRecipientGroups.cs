namespace HRM.Application.Features.PLM.Manufacturing.Services.Adjustments;

internal static class ManufacturingAdjustmentRecipientGroups
{
    // Legacy QC and PLPU planning groups; resolved to active employees in the caller's company.
    public static readonly Guid[] All = [Guid.Parse("115f897a-535b-42e9-9a62-d00ac6fcdeae"), Guid.Parse("81b58984-c3da-4fab-8faa-b28bc20738aa")];
}
