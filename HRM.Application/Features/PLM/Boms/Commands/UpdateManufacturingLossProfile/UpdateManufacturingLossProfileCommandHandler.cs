using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.Boms.Dtos;
using HRM.Application.Features.PLM.Boms.Mappers;
using HRM.Application.Features.PLM.Boms.Rules;
using HRM.Application.Features.PLM.Boms.Services;
using HRM.Domain.Enums.Boms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Boms.Commands.UpdateManufacturingLossProfile;

internal sealed class UpdateManufacturingLossProfileCommandHandler
    : IRequestHandler<UpdateManufacturingLossProfileCommand, OperationResult<ManufacturingLossProfileDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ManufacturingLossProfileWriteService _writeService;

    public UpdateManufacturingLossProfileCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        ManufacturingLossProfileWriteService writeService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _writeService = writeService;
    }

    public async Task<OperationResult<ManufacturingLossProfileDto>> Handle(
        UpdateManufacturingLossProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<ManufacturingLossProfileDto>.Fail("Current company or employee is invalid.");
        }

        var profile = await _dbContext.ManufacturingLossProfiles
            .Include(x => x.Rules)
            .FirstOrDefaultAsync(x => x.ManufacturingLossProfileId == command.ProfileId &&
                                      x.CompanyId == companyId,
                cancellationToken);
        if (profile is null)
        {
            return OperationResult<ManufacturingLossProfileDto>.Fail("Loss profile was not found.");
        }
        if (profile.Status != ManufacturingLossProfileStatus.Draft)
        {
            return OperationResult<ManufacturingLossProfileDto>.Fail("Only Draft loss profiles can be changed.");
        }

        var (error, rules) = await _writeService.ValidateAndBuildRulesAsync(
            profile.ManufacturingLossProfileId, companyId, command.Request, cancellationToken);
        if (error is not null)
        {
            return OperationResult<ManufacturingLossProfileDto>.Fail(error);
        }

        _dbContext.ManufacturingLossProfileRules.RemoveRange(profile.Rules);
        profile.Name = command.Request.Name.Trim();
        profile.EffectiveFrom = command.Request.EffectiveFrom;
        profile.EffectiveTo = command.Request.EffectiveTo;
        profile.Description = BomRules.NormalizeOptionalText(command.Request.Description);
        profile.UpdatedDate = DateTime.Now;
        profile.UpdatedBy = employeeId;
        profile.Rules = rules;
        await _dbContext.ManufacturingLossProfileRules.AddRangeAsync(rules, cancellationToken);
        _dbContext.AuditLogs.Add(BomAudit.Create(
            companyId, employeeId, "manufacturing_loss_profiles", profile.ManufacturingLossProfileId,
            "UpdateManufacturingLossProfile", new { profile.ExternalId, profile.Name, RuleCount = rules.Count }));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return OperationResult<ManufacturingLossProfileDto>.Ok(ManufacturingLossProfileMapper.ToDto(profile));
    }
}
