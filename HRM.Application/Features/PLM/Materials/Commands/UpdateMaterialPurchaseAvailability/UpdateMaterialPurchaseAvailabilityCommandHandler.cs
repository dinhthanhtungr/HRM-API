using System.Text.Json;
using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Features.Notifications.Dtos;
using HRM.Application.Features.Notifications.Services;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Materials;
using HRM.Domain.Enums.Notifications;
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
    private readonly INotificationService _notificationService;

    public UpdateMaterialPurchaseAvailabilityCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _notificationService = notificationService;
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
                Availability = x.PurchaseAvailability,
                x.ExternalId,
                x.CustomCode,
                x.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (material is null)
        {
            return new UpdateMaterialPurchaseAvailabilityResult(
                UpdateMaterialPurchaseAvailabilityOutcome.NotFound);
        }

        var now = _dateTimeProvider.Now;
        var previousStatus = material.Availability?.Status ?? MaterialPurchaseStatus.Available;
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

        var shouldNotifyLab = previousStatus != MaterialPurchaseStatus.Unavailable &&
            availability.Status == MaterialPurchaseStatus.Unavailable;

        if (shouldNotifyLab)
        {
            var materialCode = FirstNotEmpty(material.ExternalId, material.CustomCode, material.Name)
                ?? command.MaterialId.ToString();
            var materialLabel = TrimToMaxLength(materialCode, 180);
            var materialLink = $"/plm/material-price-reviews?materialId={command.MaterialId}";

            await _notificationService.PublishAsync(new PublishNotificationRequest
            {
                CompanyId = companyId,
                CreatedBy = employeeId,
                CreatedByNameSnapshot = _currentUser.UserName,
                Topic = TopicNotifications.MaterialPurchaseUnavailable,
                Severity = NotificationSeverity.Warning,
                Title = "Ngừng mua NVL",
                Message = $"{materialLabel} không được dùng cho công thức mới từ {availability.EffectiveFrom:dd/MM/yyyy}.",
                Link = materialLink,
                AggregateId = command.MaterialId,
                AggregateCode = materialLabel,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    contentType = "material_purchase_availability_changed",
                    material = new
                    {
                        id = command.MaterialId,
                        code = materialLabel,
                        name = material.Name
                    },
                    availability = new
                    {
                        status = availability.Status.ToString(),
                        reason = availability.Reason,
                        effectiveFrom = availability.EffectiveFrom,
                        expectedAvailableDate = availability.ExpectedAvailableDate
                    },
                    action = new { href = materialLink }
                }),
                TargetRoles = ApplicationRoleSets.PLM.MaterialAvailabilityLabRecipients
            }, cancellationToken);
        }
        else
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

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
                NotificationPublished = shouldNotifyLab
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

    private static string? FirstNotEmpty(params string?[] values)
        => values.Select(Normalize).FirstOrDefault(x => x is not null);

    private static string TrimToMaxLength(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static UpdateMaterialPurchaseAvailabilityResult Invalid(string message)
        => new(UpdateMaterialPurchaseAvailabilityOutcome.InvalidRequest, Message: message);
}
