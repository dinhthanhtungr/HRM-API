using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Materials;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Commands.UpdateMaterialPurchaseAvailability;

internal sealed class UpdateMaterialPurchaseAvailabilityCommandHandler
    : IRequestHandler<UpdateMaterialPurchaseAvailabilityCommand, UpdateMaterialPurchaseAvailabilityResult>
{
    private const int MaxReasonLength = 1_000;
    private const int MaxNoteLength = 2_000;

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdateMaterialPurchaseAvailabilityCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<UpdateMaterialPurchaseAvailabilityResult> Handle(
        UpdateMaterialPurchaseAvailabilityCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return Invalid("Current company or employee is invalid.");
        }

        var validationError = Validate(command);
        if (validationError is not null)
        {
            return Invalid(validationError);
        }

        var material = await _dbContext.Materials
            .Where(x =>
                x.MaterialId == command.MaterialId &&
                x.CompanyId == companyId &&
                x.IsActive == true)
            .Select(x => new
            {
                Availability = x.PurchaseAvailability
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (material is null)
        {
            return new UpdateMaterialPurchaseAvailabilityResult(
                UpdateMaterialPurchaseAvailabilityOutcome.NotFound);
        }

        var now = _dateTimeProvider.Now;
        var availability = material.Availability;

        if (availability is null)
        {
            availability = new MaterialPurchaseAvailability
            {
                MaterialPurchaseAvailabilityId = Guid.CreateVersion7(),
                MaterialId = command.MaterialId,
                CreatedBy = employeeId,
                CreatedDate = now
            };
            await _dbContext.MaterialPurchaseAvailabilities.AddAsync(availability, cancellationToken);
        }

        var request = command.Request;
        availability.Status = request.Status;
        availability.Reason = Normalize(request.Reason);
        availability.EffectiveFrom = request.Status == MaterialPurchaseStatus.Unavailable
            ? request.EffectiveFrom ?? now
            : null;
        availability.ExpectedAvailableDate = request.Status == MaterialPurchaseStatus.Unavailable
            ? request.ExpectedAvailableDate
            : null;
        availability.Note = Normalize(request.Note);
        availability.UpdatedBy = employeeId;
        availability.UpdatedDate = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UpdateMaterialPurchaseAvailabilityResult(
            UpdateMaterialPurchaseAvailabilityOutcome.Updated,
            new MaterialPurchaseAvailabilityDto
            {
                MaterialId = command.MaterialId,
                Status = availability.Status,
                Reason = availability.Reason,
                EffectiveFrom = availability.EffectiveFrom,
                ExpectedAvailableDate = availability.ExpectedAvailableDate,
                Note = availability.Note,
                UpdatedBy = availability.UpdatedBy,
                UpdatedDate = availability.UpdatedDate,
                NotificationPublished = false
            });
    }

    private static string? Validate(UpdateMaterialPurchaseAvailabilityCommand command)
    {
        if (command.MaterialId == Guid.Empty)
        {
            return "MaterialId is required.";
        }

        if (!Enum.IsDefined(command.Request.Status))
        {
            return "Purchase status is invalid.";
        }

        var reason = Normalize(command.Request.Reason);
        if (command.Request.Status == MaterialPurchaseStatus.Unavailable && reason is null)
        {
            return "Reason is required when material is unavailable.";
        }

        if (reason?.Length > MaxReasonLength)
        {
            return $"Reason must not exceed {MaxReasonLength} characters.";
        }

        if (Normalize(command.Request.Note)?.Length > MaxNoteLength)
        {
            return $"Note must not exceed {MaxNoteLength} characters.";
        }

        if (command.Request.Status == MaterialPurchaseStatus.Unavailable &&
            command.Request.EffectiveFrom.HasValue &&
            command.Request.ExpectedAvailableDate.HasValue &&
            command.Request.ExpectedAvailableDate.Value < command.Request.EffectiveFrom.Value)
        {
            return "ExpectedAvailableDate must be on or after EffectiveFrom.";
        }

        return null;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static UpdateMaterialPurchaseAvailabilityResult Invalid(string message)
        => new(UpdateMaterialPurchaseAvailabilityOutcome.InvalidRequest, Message: message);
}
