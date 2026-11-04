using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Concurrency;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.DevAndQA.Shared.Evaluations.Services;
using HRM.Application.Features.DevAndQA.IQC.Services.Evaluations;
using HRM.Application.Features.DevAndQA.CoaCertificates.Services;
using HRM.Application.Features.DevAndQA.EvaluationTemplates.Services;
using HRM.Application.Features.DevAndQA.MeasurementMethods.Services;

namespace HRM.Application.Tests.Features.DevAndQA;

/// <summary>Ghép các service độc lập với cùng context/lock cho regression tests, không phải service production.</summary>
internal sealed class EvaluationServicesFixture
{
    public MeasurementMethodService Methods { get; }
    public EvaluationTemplateService Templates { get; }
    public IqcEvaluationService Iqc { get; }
    public CoaCertificateService Coa { get; }

    public EvaluationServicesFixture(IEvaluationDbContext db, ICurrentUser user,
        ICurrentUserPermissionService permissions, KeyedMutationLock<string> mutationLock)
    {
        var access = new EvaluationAccessService(db, user, permissions);
        Methods = new(db, access, mutationLock);
        Templates = new(db, access, mutationLock);
        Iqc = new(db, access, mutationLock, Templates);
        Coa = new(db, access, mutationLock, Templates);
    }
}
