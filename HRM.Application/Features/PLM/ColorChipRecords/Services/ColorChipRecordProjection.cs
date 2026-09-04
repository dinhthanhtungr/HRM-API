using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Domain.Entities.SampleRequestSchema;
using System.Linq.Expressions;

namespace HRM.Application.Features.PLM.ColorChipRecords.Services;

internal static class ColorChipRecordProjection
{
    public static readonly Expression<Func<ColorChipRecord, ColorChipRecordDto>> ToDto = entity =>
        new ColorChipRecordDto
        {
            ColorChipRecordId = entity.ColorChipRecordId,
            RecordType = entity.RecordType,
            ResinType = entity.ResinType,
            LogoType = entity.LogoType,
            FormStyle = entity.FormStyle,
            ProductId = entity.ProductId,
            ProductName = entity.Product == null ? null : entity.Product.Name,
            ProductExternalId = entity.Product == null ? null : entity.Product.ColourCode,
            Machine = entity.Machine,
            Resin = entity.Resin,
            TemperatureLimit = entity.TemperatureLimit,
            SizeText = entity.SizeText,
            PelletWeightGram = entity.PelletWeightGram,
            NetWeightGram = entity.NetWeightGram,
            Electrostatic = entity.Electrostatic,
            Lightness = entity.Lightness,
            AValue = entity.AValue,
            BValue = entity.BValue,
            AttachmentCollectionId = entity.AttachmentCollectionId,
            RecordDate = entity.RecordDate,
            Note = entity.Note,
            PrintNote = entity.PrintNote,
            CreatedDate = entity.CreatedDate,
            CreatedBy = entity.CreatedBy,
            UpdatedDate = entity.UpdatedDate,
            UpdatedBy = entity.UpdatedBy,
            CompanyId = entity.CompanyId,
            IsActive = entity.IsActive,
            DevelopmentFormulas = entity.DevelopmentFormulas
                .Where(link => link.IsActive && link.DevelopmentFormula != null && link.DevelopmentFormula.IsActive)
                .OrderBy(link => link.DevelopmentFormula!.ExternalId)
                .Select(link => new ColorChipRecordDevelopmentFormulaDto
                {
                    ColorChipRecordDevelopmentFormulaId = link.ColorChipRecordDevelopmentFormulaId,
                    DevelopmentFormulaId = link.DevelopmentFormulaId,
                    IsActive = link.IsActive,
                    DevelopmentFormulaExternalId = link.DevelopmentFormula!.ExternalId,
                    DevelopmentFormulaName = link.DevelopmentFormula.Name
                })
                .ToList()
        };
}
