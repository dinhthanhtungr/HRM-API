using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Materials.Commands;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Materials;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Commands.CreateMaterialReplacement;

internal sealed class CreateMaterialReplacementCommandHandler
    : IRequestHandler<CreateMaterialReplacementCommand, MaterialReplacementWriteResult>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateMaterialReplacementCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<MaterialReplacementWriteResult> Handle(
        CreateMaterialReplacementCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SourceMaterialId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return Invalid("Current company, employee, or source material is invalid.");
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

        if (request.ReplacementMaterialId == command.SourceMaterialId)
        {
            return Invalid("SourceMaterialId and ReplacementMaterialId must be different.");
        }

        var materialsExist = await _dbContext.Materials
            .Where(x => x.CompanyId == companyId && x.IsActive == true &&
                        (x.MaterialId == command.SourceMaterialId || x.MaterialId == request.ReplacementMaterialId))
            .Select(x => x.MaterialId)
            .ToListAsync(cancellationToken);
        if (materialsExist.Count != 2)
        {
            return new MaterialReplacementWriteResult(MaterialReplacementWriteOutcome.NotFound);
        }

        var now = _dateTimeProvider.Now;
        var replacement = await _dbContext.MaterialReplacements
            .FirstOrDefaultAsync(x =>
                x.SourceMaterialId == command.SourceMaterialId &&
                x.ReplacementMaterialId == request.ReplacementMaterialId,
                cancellationToken);

        var outcome = MaterialReplacementWriteOutcome.Created;
        if (replacement is null)
        {
            replacement = new MaterialReplacement
            {
                MaterialReplacementId = Guid.CreateVersion7(),
                SourceMaterialId = command.SourceMaterialId,
                ReplacementMaterialId = request.ReplacementMaterialId,
                CreatedBy = employeeId,
                CreatedDate = now
            };
            await _dbContext.MaterialReplacements.AddAsync(replacement, cancellationToken);
        }
        else
        {
            outcome = MaterialReplacementWriteOutcome.Updated;
            replacement.UpdatedBy = employeeId;
            replacement.UpdatedDate = now;
        }

        replacement.ApplicableContext = MaterialReplacementWriteSupport.ToDocument(request.ApplicableContext);
        replacement.TechnicalNote = MaterialReplacementWriteSupport.Normalize(request.TechnicalNote);
        replacement.ReplacementRatio = request.ReplacementRatio;
        replacement.Priority = request.Priority;
        replacement.IsRecommended = request.IsRecommended;
        replacement.IsActive = true;

        await _dbContext.SaveChangesAsync(cancellationToken);
        var data = await LoadDtoAsync(replacement.MaterialReplacementId, companyId, cancellationToken);
        return new MaterialReplacementWriteResult(outcome, data);
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
