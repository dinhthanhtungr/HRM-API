using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Materials.Commands;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Domain.Enums.Materials;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Commands.UpdateMaterialReplacement;

internal sealed class UpdateMaterialReplacementCommandHandler
    : IRequestHandler<UpdateMaterialReplacementCommand, MaterialReplacementWriteResult>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateMaterialReplacementCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<MaterialReplacementWriteResult> Handle(
        UpdateMaterialReplacementCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MaterialReplacementId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return Invalid("Current company, employee, or material replacement is invalid.");
        }

        var request = command.Request;
        var validationError = MaterialReplacementWriteSupport.Validate(
            request.ReplacementMaterialId,
            request.ApplicableContext,
            request.TechnicalNote,
            request.ReplacementRatio,
            request.Priority);
        if (validationError is not null)
        {
            return Invalid(validationError);
        }

        var replacement = await _dbContext.MaterialReplacements
            .Include(x => x.SourceMaterial)
            .FirstOrDefaultAsync(x =>
                x.MaterialReplacementId == command.MaterialReplacementId &&
                x.SourceMaterial.CompanyId == companyId &&
                x.SourceMaterial.IsActive == true,
                cancellationToken);
        if (replacement is null)
        {
            return new MaterialReplacementWriteResult(MaterialReplacementWriteOutcome.NotFound);
        }

        if (replacement.SourceMaterialId == request.ReplacementMaterialId)
        {
            return Invalid("SourceMaterialId and ReplacementMaterialId must be different.");
        }

        var replacementMaterialExists = await _dbContext.Materials.AnyAsync(x =>
            x.MaterialId == request.ReplacementMaterialId &&
            x.CompanyId == companyId &&
            x.IsActive == true,
            cancellationToken);
        if (!replacementMaterialExists)
        {
            return new MaterialReplacementWriteResult(MaterialReplacementWriteOutcome.NotFound);
        }

        var duplicateExists = await _dbContext.MaterialReplacements.AnyAsync(x =>
            x.MaterialReplacementId != replacement.MaterialReplacementId &&
            x.SourceMaterialId == replacement.SourceMaterialId &&
            x.ReplacementMaterialId == request.ReplacementMaterialId,
            cancellationToken);
        if (duplicateExists)
        {
            return Invalid("A replacement option for this material already exists.");
        }

        replacement.ReplacementMaterialId = request.ReplacementMaterialId;
        replacement.ApplicableContext = MaterialReplacementWriteSupport.ToDocument(request.ApplicableContext);
        replacement.TechnicalNote = MaterialReplacementWriteSupport.Normalize(request.TechnicalNote);
        replacement.ReplacementRatio = request.ReplacementRatio;
        replacement.Priority = request.Priority;
        replacement.IsRecommended = request.IsRecommended;
        replacement.IsActive = request.IsActive;
        replacement.UpdatedBy = employeeId;
        replacement.UpdatedDate = _dateTimeProvider.Now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        var data = await LoadDtoAsync(replacement.MaterialReplacementId, companyId, cancellationToken);
        return new MaterialReplacementWriteResult(MaterialReplacementWriteOutcome.Updated, data);
    }

    private async Task<MaterialReplacementDto?> LoadDtoAsync(Guid id, Guid companyId, CancellationToken cancellationToken)
    {
        var row = await _dbContext.MaterialReplacements.AsNoTracking()
            .Where(x => x.MaterialReplacementId == id && x.SourceMaterial.CompanyId == companyId && x.ReplacementMaterial.CompanyId == companyId)
            .Select(x => new MaterialReplacementProjection
            {
                MaterialReplacementId = x.MaterialReplacementId, SourceMaterialId = x.SourceMaterialId,
                ReplacementMaterialId = x.ReplacementMaterialId, ExternalId = x.ReplacementMaterial.ExternalId,
                CustomCode = x.ReplacementMaterial.CustomCode, Name = x.ReplacementMaterial.Name,
                PurchaseStatus = x.ReplacementMaterial.PurchaseAvailability == null ? MaterialPurchaseStatus.Available : x.ReplacementMaterial.PurchaseAvailability.Status,
                ApplicableContext = x.ApplicableContext, TechnicalNote = x.TechnicalNote,
                ReplacementRatio = x.ReplacementRatio, Priority = x.Priority, IsRecommended = x.IsRecommended,
                IsActive = x.IsActive, CreatedDate = x.CreatedDate, UpdatedDate = x.UpdatedDate
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : MaterialReplacementWriteSupport.ToDto(row);
    }

    private static MaterialReplacementWriteResult Invalid(string message)
        => new(MaterialReplacementWriteOutcome.InvalidRequest, Message: message);
}
