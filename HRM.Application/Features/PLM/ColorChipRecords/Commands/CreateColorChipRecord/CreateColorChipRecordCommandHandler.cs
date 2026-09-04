using HRM.Application.Abstractions.Commons.Time;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Application.Features.PLM.ColorChipRecords.Services;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ColorChipRecords.Commands.CreateColorChipRecord;

internal sealed class CreateColorChipRecordCommandHandler
    : IRequestHandler<CreateColorChipRecordCommand, OperationResult<SaveColorChipRecordResultDto>>
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateColorChipRecordCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<OperationResult<SaveColorChipRecordResultDto>> Handle(
        CreateColorChipRecordCommand request,
        CancellationToken cancellationToken)
    {
        var employeeId = _currentUser.EmployeeId.GetValueOrDefault();
        var companyId = _currentUser.CompanyId.GetValueOrDefault();
        if (employeeId == Guid.Empty || companyId == Guid.Empty)
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail("Current employee or company is invalid.");
        }

        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail(validationError);
        }

        var formulaResult = ColorChipRecordRules.ValidateDevelopmentFormulaIds(request.DevelopmentFormulaIds);
        if (!formulaResult.Success)
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail(
                formulaResult.Message ?? "DevelopmentFormulaIds is invalid.");
        }

        var productExists = await _dbContext.Products
            .AsNoTracking()
            .AnyAsync(x =>
                x.ProductId == request.ProductId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);
        if (!productExists)
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail(
                "Product was not found, is inactive, or is outside the current company.");
        }

        var recordExists = await _dbContext.ColorChipRecords
            .AsNoTracking()
            .AnyAsync(x =>
                x.ProductId == request.ProductId &&
                x.CompanyId == companyId &&
                x.IsActive,
                cancellationToken);
        if (recordExists)
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail(
                "An active color chip record already exists for this product. Use PATCH to update it.");
        }

        if (formulaResult.Data is { } formulaId &&
            !await FormulaExistsAsync(formulaId, request.ProductId, companyId, cancellationToken))
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail(
                "Development formula was not found or does not belong to this product and company.");
        }

        var attachmentCollectionResult = await ResolveAttachmentCollectionIdAsync(
            request.AttachmentCollectionId,
            cancellationToken);
        if (!attachmentCollectionResult.Success)
        {
            return OperationResult<SaveColorChipRecordResultDto>.Fail(
                attachmentCollectionResult.Message ?? "AttachmentCollectionId is invalid.");
        }

        var now = _dateTimeProvider.Now;
        var entity = new ColorChipRecord
        {
            ColorChipRecordId = Guid.CreateVersion7(),
            RecordType = request.RecordType,
            ResinType = request.ResinType,
            LogoType = request.LogoType,
            FormStyle = request.FormStyle,
            ProductId = request.ProductId,
            Machine = ColorChipRecordRules.TrimToNull(request.Machine),
            Resin = ColorChipRecordRules.TrimToNull(request.Resin),
            TemperatureLimit = ColorChipRecordRules.TrimToNull(request.TemperatureLimit),
            SizeText = ColorChipRecordRules.TrimToNull(request.SizeText),
            PelletWeightGram = request.PelletWeightGram,
            NetWeightGram = ColorChipRecordRules.TrimToNull(request.NetWeightGram),
            Electrostatic = request.Electrostatic,
            Lightness = request.Lightness,
            AValue = request.AValue,
            BValue = request.BValue,
            AttachmentCollectionId = attachmentCollectionResult.Data,
            RecordDate = request.RecordDate,
            Note = ColorChipRecordRules.TrimToNull(request.Note),
            PrintNote = ColorChipRecordRules.TrimToNull(request.PrintNote),
            CreatedDate = now,
            CreatedBy = employeeId,
            UpdatedDate = now,
            UpdatedBy = employeeId,
            CompanyId = companyId,
            IsActive = true
        };

        if (formulaResult.Data is { } linkedFormulaId)
        {
            entity.DevelopmentFormulas.Add(new ColorChipRecordDevelopmentFormula
            {
                ColorChipRecordDevelopmentFormulaId = Guid.CreateVersion7(),
                ColorChipRecordId = entity.ColorChipRecordId,
                DevelopmentFormulaId = linkedFormulaId,
                IsActive = true
            });
        }

        await _dbContext.ColorChipRecords.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<SaveColorChipRecordResultDto>.Ok(
            new SaveColorChipRecordResultDto
            {
                ColorChipRecordId = entity.ColorChipRecordId,
                ProductId = request.ProductId,
                AttachmentCollectionId = entity.AttachmentCollectionId,
                UpdatedDate = entity.UpdatedDate
            },
            "Created color chip record successfully.");
    }

    private async Task<OperationResult<Guid>> ResolveAttachmentCollectionIdAsync(
        Guid? requestedCollectionId,
        CancellationToken cancellationToken)
    {
        if (!requestedCollectionId.HasValue)
        {
            var collectionId = Guid.CreateVersion7();
            await _dbContext.AttachmentCollections.AddAsync(
                new AttachmentCollection { AttachmentCollectionId = collectionId },
                cancellationToken);
            return OperationResult<Guid>.Ok(collectionId);
        }

        if (requestedCollectionId == Guid.Empty)
        {
            return OperationResult<Guid>.Fail("AttachmentCollectionId is invalid.");
        }

        var collectionIsAvailable = await _dbContext.AttachmentCollections
            .AsNoTracking()
            .AnyAsync(x =>
                x.AttachmentCollectionId == requestedCollectionId &&
                !x.Attachments.Any(attachment => attachment.IsActive) &&
                !_dbContext.ColorChipRecords.Any(record =>
                    record.AttachmentCollectionId == requestedCollectionId && record.IsActive),
                cancellationToken);
        return collectionIsAvailable
            ? OperationResult<Guid>.Ok(requestedCollectionId.Value)
            : OperationResult<Guid>.Fail(
                "Attachment collection was not found, already contains files, or is already assigned.");
    }

    private Task<bool> FormulaExistsAsync(
        Guid formulaId,
        Guid productId,
        Guid companyId,
        CancellationToken cancellationToken)
        => _dbContext.Formulas.AsNoTracking().AnyAsync(x =>
            x.FormulaId == formulaId &&
            x.ProductId == productId &&
            x.Product.CompanyId == companyId &&
            x.IsActive &&
            (!x.CompanyId.HasValue || x.CompanyId == companyId),
            cancellationToken);

    private static string? ValidateRequest(CreateColorChipRecordCommand request)
    {
        if (request.ProductId == Guid.Empty) return "ProductId is invalid.";

        var error = ColorChipRecordRules.ValidateClassification(
            request.RecordType,
            request.ResinType,
            request.LogoType,
            request.FormStyle);
        if (error is not null) return error;

        error = ColorChipRecordRules.ValidateMeasurements(request.PelletWeightGram);
        if (error is not null) return error;

        return ValidateTextFields(
            request.Machine,
            request.Resin,
            request.TemperatureLimit,
            request.SizeText,
            request.NetWeightGram,
            request.Note,
            request.PrintNote,
            rejectBlank: false);
    }

    internal static string? ValidateTextFields(
        string? machine,
        string? resin,
        string? temperatureLimit,
        string? sizeText,
        string? netWeightGram,
        string? note,
        string? printNote,
        bool rejectBlank)
    {
        var fields = new (string Name, string? Value, int MaxLength)[]
        {
            ("Machine", machine, ColorChipRecordRules.MaxTechnicalTextLength),
            ("Resin", resin, ColorChipRecordRules.MaxTechnicalTextLength),
            ("TemperatureLimit", temperatureLimit, ColorChipRecordRules.MaxTechnicalTextLength),
            ("SizeText", sizeText, ColorChipRecordRules.MaxSizeTextLength),
            ("NetWeightGram", netWeightGram, ColorChipRecordRules.MaxSizeTextLength),
            ("Note", note, ColorChipRecordRules.MaxNoteLength),
            ("PrintNote", printNote, ColorChipRecordRules.MaxNoteLength)
        };

        return fields
            .Select(field => ColorChipRecordRules.ValidateOptionalText(
                field.Name,
                field.Value,
                field.MaxLength,
                rejectBlank))
            .FirstOrDefault(error => error is not null);
    }
}
