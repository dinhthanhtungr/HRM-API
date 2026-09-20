using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Abstractions.Commons.ExternalIds;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Entities.BomSchema;
using HRM.Domain.Enums.Audits;
using HRM.Domain.Enums.Category;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.CreateManufacturingLossProfile;

internal sealed class CreateManufacturingLossProfileCommandHandler
    : IRequestHandler<CreateManufacturingLossProfileCommand, OperationResult<ManufacturingLossProfileDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ManufacturingLossProfileWriteService _writeService;
    private readonly IExternalIdService _externalIdService;

    public CreateManufacturingLossProfileCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        ManufacturingLossProfileWriteService writeService,
        IExternalIdService externalIdService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _writeService = writeService;
        _externalIdService = externalIdService;
    }

    public async Task<OperationResult<ManufacturingLossProfileDto>> Handle(
        CreateManufacturingLossProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<ManufacturingLossProfileDto>.Fail("Current company or employee is invalid.");
        }

        var profileId = Guid.CreateVersion7();
        var (error, rules) = await _writeService.ValidateAndBuildRulesAsync(
            profileId, companyId, command.Request, cancellationToken);
        if (error is not null)
        {
            return OperationResult<ManufacturingLossProfileDto>.Fail(error);
        }

        var code = await _externalIdService.GenerateGlobalCodeAsync(
            companyId, DocumentPrefix.DHH.ToString(), cancellationToken);

        var profile = new ManufacturingLossProfile
        {
            ManufacturingLossProfileId = profileId,
            CompanyId = companyId,
            ExternalId = code,
            Name = command.Request.Name.Trim(),
            EffectiveFrom = command.Request.EffectiveFrom,
            EffectiveTo = command.Request.EffectiveTo,
            Description = BomRules.NormalizeOptionalText(command.Request.Description),
            CreatedDate = DateTime.Now,
            CreatedBy = employeeId,
            Rules = rules
        };
        await _dbContext.ManufacturingLossProfiles.AddAsync(profile, cancellationToken);
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId, employeeId, "manufacturing_loss_profiles", profileId,
            "CreateManufacturingLossProfile", new { profile.ExternalId, profile.Name, RuleCount = rules.Count },
            actionType: AuditActionType.Create));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<ManufacturingLossProfileDto>.Ok(ManufacturingLossProfileMapper.ToDto(profile));
    }
}
