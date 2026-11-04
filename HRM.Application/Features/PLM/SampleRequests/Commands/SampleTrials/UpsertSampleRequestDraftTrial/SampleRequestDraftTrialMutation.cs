using HRM.Application.Commons.Patching;
using HRM.Application.Features.PLM.SampleRequests.SampleTrials;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.UpsertSampleRequestDraftTrial;

internal static class SampleRequestDraftTrialMutation
{
    public static string? ValidateInput(UpsertSampleRequestDraftTrialCommand request)
        => SampleRequestSampleTrialMutationRules.ValidateText(
               request.FormulaExternalId,
               SampleRequestSampleTrialMutationRules.MaxBatchNoLength,
               nameof(request.FormulaExternalId))
           ?? SampleRequestSampleTrialMutationRules.ValidateText(
               request.BatchNo,
               SampleRequestSampleTrialMutationRules.MaxBatchNoLength,
               nameof(request.BatchNo))
           ?? SampleRequestSampleTrialMutationRules.ValidateText(
               request.DeliveryMethod,
               SampleRequestSampleTrialMutationRules.MaxDeliveryMethodLength,
               nameof(request.DeliveryMethod))
           ?? SampleRequestSampleTrialMutationRules.ValidateText(
               request.LabNote,
               SampleRequestSampleTrialMutationRules.MaxLabNoteLength,
               nameof(request.LabNote));

    public static string? ValidateProposedValues(
        SampleRequestSampleTrial? trial,
        UpsertSampleRequestDraftTrialCommand request,
        IReadOnlySet<string> clearFields)
    {
        var deliveredQuantity = ResolveNullable(
            trial?.DeliveredSampleQuantityKg,
            request.DeliveredSampleQuantityKg,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.DeliveredSampleQuantityKg));
        return SampleRequestSampleTrialMutationRules.Validate(
            deliveredQuantity,
            additiveRate: null,
            requestReceivedDate: null,
            finishedDate: null,
            sentDate: null);
    }

    public static bool ApplyTrial(
        SampleRequestSampleTrial trial,
        UpsertSampleRequestDraftTrialCommand request,
        IReadOnlySet<string> clearFields,
        string? formulaExternalId)
    {
        var changed = false;
        changed |= ApplyNullable(
            request.FormulaId,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.FormulaId),
            () => trial.FormulaId,
            value => trial.FormulaId = value);

        if (request.FormulaId.HasValue || request.FormulaExternalId is not null)
        {
            changed |= PatchHelper.SetTrimmed(
                formulaExternalId,
                () => trial.BatchNo,
                value => trial.BatchNo = value);
        }
        else
        {
            changed |= ApplyString(
                request.BatchNo,
                clearFields.Contains(SampleRequestSampleTrialPatchFields.BatchNo),
                () => trial.BatchNo,
                value => trial.BatchNo = value);
        }

        changed |= ApplyNullable(
            request.DeliveredSampleQuantityKg,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.DeliveredSampleQuantityKg),
            () => trial.DeliveredSampleQuantityKg,
            value => trial.DeliveredSampleQuantityKg = value);
        changed |= ApplyString(
            request.DeliveryMethod,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.DeliveryMethod),
            () => trial.DeliveryMethod,
            value => trial.DeliveryMethod = value);
        changed |= ApplyString(
            request.LabNote,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.LabNote),
            () => trial.LabNote,
            value => trial.LabNote = value);
        return changed;
    }

    public static bool ApplySampleRequestDeliveryDates(
        SampleRequest sampleRequest,
        UpsertSampleRequestDraftTrialCommand request,
        IReadOnlySet<string> clearFields)
    {
        var changed = false;
        changed |= ApplyNullable(
            request.RequestDeliveryDate,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.RequestDeliveryDate),
            () => sampleRequest.RequestDeliveryDate,
            value => sampleRequest.RequestDeliveryDate = value);
        changed |= ApplyNullable(
            request.ExpectedDeliveryDate,
            clearFields.Contains(SampleRequestSampleTrialPatchFields.ExpectedDeliveryDate),
            () => sampleRequest.ExpectedDeliveryDate,
            value => sampleRequest.ExpectedDeliveryDate = value);
        return changed;
    }

    public static IReadOnlyList<string> GetFieldsWithValues(
        UpsertSampleRequestDraftTrialCommand request)
    {
        var fields = new List<string>();
        AddIf(fields, SampleRequestSampleTrialPatchFields.FormulaId, request.FormulaId.HasValue);
        AddIf(fields, SampleRequestSampleTrialPatchFields.FormulaExternalId, request.FormulaExternalId is not null);
        AddIf(fields, SampleRequestSampleTrialPatchFields.BatchNo, request.BatchNo is not null);
        AddIf(fields, SampleRequestSampleTrialPatchFields.DeliveredSampleQuantityKg, request.DeliveredSampleQuantityKg.HasValue);
        AddIf(fields, SampleRequestSampleTrialPatchFields.RequestDeliveryDate, request.RequestDeliveryDate.HasValue);
        AddIf(fields, SampleRequestSampleTrialPatchFields.ExpectedDeliveryDate, request.ExpectedDeliveryDate.HasValue);
        AddIf(fields, SampleRequestSampleTrialPatchFields.DeliveryMethod, request.DeliveryMethod is not null);
        AddIf(fields, SampleRequestSampleTrialPatchFields.LabNote, request.LabNote is not null);
        return fields;
    }

    private static bool ApplyNullable<T>(
        T? incoming,
        bool clear,
        Func<T?> current,
        Action<T?> apply)
        where T : struct
        => clear
            ? PatchHelper.SetNullable<T>(null, current, apply)
            : incoming.HasValue && PatchHelper.SetNullable(incoming, current, apply);

    private static bool ApplyString(
        string? incoming,
        bool clear,
        Func<string?> current,
        Action<string?> apply)
        => clear
            ? PatchHelper.SetNullableRef<string>(null, current, apply)
            : PatchHelper.SetTrimmed(incoming, current, apply);

    private static T? ResolveNullable<T>(T? current, T? incoming, bool clear)
        where T : struct
        => clear ? null : incoming ?? current;

    private static void AddIf(ICollection<string> fields, string field, bool condition)
    {
        if (condition)
        {
            fields.Add(field);
        }
    }
}
