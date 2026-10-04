using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Rules;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Queries.ValidateManufacturingProcessTemplateRelease;

internal sealed class ValidateManufacturingProcessTemplateReleaseQueryHandler
    : IRequestHandler<ValidateManufacturingProcessTemplateReleaseQuery, ManufacturingProcessTemplateValidationDto?>
{
    private readonly IPLMReadDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ValidateManufacturingProcessTemplateReleaseQueryHandler(IPLMReadDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ManufacturingProcessTemplateValidationDto?> Handle(
        ValidateManufacturingProcessTemplateReleaseQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
            return null;

        var entity = await _db.ManufacturingProcessTemplates.AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.ApplicabilityRules)
                .ThenInclude(x => x.Category)
            .Include(x => x.Stages)
                .ThenInclude(x => x.WorkInstructionTemplate)
            .Include(x => x.Stages)
                .ThenInclude(x => x.Machines)
                .ThenInclude(x => x.Parameters)
            .Include(x => x.StageTransitions)
            .FirstOrDefaultAsync(x => x.ManufacturingProcessTemplateId == request.TemplateId 
                                   && x.CompanyId == companyId 
                                   && x.IsActive, cancellationToken);

        if (entity is null)
            return null;

        var draftInstructionIds = entity.Stages
            .Select(stage => stage.WorkInstructionTemplate)
            .Where(instruction => instruction?.Status == HRM.Domain.Enums.Boms.ManufacturingTemplateStatus.Draft)
            .Select(instruction => instruction!.ManufacturingWorkInstructionTemplateId)
            .Distinct()
            .ToList();
        var sharedDraftInstructionIds = draftInstructionIds.Count == 0
            ? new HashSet<Guid>()
            : await _db.ManufacturingProcessTemplateStages
                .AsNoTracking()
                .Where(stage =>
                    stage.ManufacturingWorkInstructionTemplateId.HasValue &&
                    draftInstructionIds.Contains(stage.ManufacturingWorkInstructionTemplateId.Value) &&
                    stage.ManufacturingProcessTemplateId != entity.ManufacturingProcessTemplateId &&
                    stage.ProcessTemplate.CompanyId == companyId)
                .Select(stage => stage.ManufacturingWorkInstructionTemplateId!.Value)
                .Distinct()
                .ToHashSetAsync(cancellationToken);

        return new ManufacturingProcessTemplateValidationDto
        {
            Issues = ManufacturingProcessTemplateReleaseValidator.Validate(
                entity,
                DateTime.Now,
                sharedDraftInstructionIds)
        };
    }
}
