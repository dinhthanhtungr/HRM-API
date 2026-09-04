using HRM.Application.Abstractions.Documents;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.ColorChipRecords.Queries.ExportColorChipRecordPdf;

internal sealed class ExportColorChipRecordPdfQueryHandler
    : IRequestHandler<ExportColorChipRecordPdfQuery, OperationResult<ColorChipRecordPdfFileDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IColorChipRecordPdfRenderer _renderer;

    public ExportColorChipRecordPdfQueryHandler(
        IPLMReadDbContext dbContext,
        ICurrentUser currentUser,
        IColorChipRecordPdfRenderer renderer)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _renderer = renderer;
    }

    public async Task<OperationResult<ColorChipRecordPdfFileDto>> Handle(
        ExportColorChipRecordPdfQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ProductId == Guid.Empty)
        {
            return OperationResult<ColorChipRecordPdfFileDto>.Fail("ProductId is invalid.");
        }

        if (_currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return OperationResult<ColorChipRecordPdfFileDto>.Fail("Current company is invalid.");
        }

        var customerName = await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                x.ProductId == request.ProductId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Customer.CompanyId == companyId)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => x.Customer.CustomerName)
            .FirstOrDefaultAsync(cancellationToken);

        var data = await _dbContext.ColorChipRecords
            .AsNoTracking()
            .Where(x =>
                x.ProductId == request.ProductId &&
                x.CompanyId == companyId &&
                x.IsActive &&
                x.Product != null &&
                x.Product.CompanyId == companyId &&
                x.Product.IsActive)
            .OrderByDescending(x => x.CreatedDate)
            .Select(x => new
            {
                x.ColorChipRecordId,
                x.FormStyle,
                x.RecordDate,
                x.CreatedDate,
                x.Resin,
                x.ResinType,
                x.RecordType,
                x.LogoType,
                x.Machine,
                x.TemperatureLimit,
                x.SizeText,
                x.PelletWeightGram,
                x.NetWeightGram,
                x.Electrostatic,
                x.Note,
                x.PrintNote,
                ProductColourCode = x.Product!.ColourCode,
                ProductName = x.Product.Name,
                ProductUsageRate = x.Product.UsageRate,
                ProductDeltaE = x.Product.DeltaE,
                ProductCreatedByName = x.Product.CreatedByNavigation != null
                    ? x.Product.CreatedByNavigation.FullName
                    : null,
                FirstDevelopmentFormulaCode = x.DevelopmentFormulas
                    .Where(df => df.IsActive && df.DevelopmentFormula != null)
                    .Select(df => df.DevelopmentFormula!.ExternalId)
                    .FirstOrDefault(),
                FirstPreparedByName = x.DevelopmentFormulas
                    .Where(df => df.IsActive && df.DevelopmentFormula != null)
                    .Select(df => df.DevelopmentFormula!.CreatedByNavigation != null
                        ? df.DevelopmentFormula.CreatedByNavigation.FullName
                        : null)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (data is null)
        {
            return OperationResult<ColorChipRecordPdfFileDto>
                .Fail("Color chip record was not found or is outside your company.");
        }

        var developmentFormulaCodes = await _dbContext.ColorChipRecordDevelopmentFormulas
            .AsNoTracking()
            .Where(df =>
                df.ColorChipRecordId == data.ColorChipRecordId &&
                df.IsActive &&
                df.DevelopmentFormula != null &&
                df.ColorChipRecord.CompanyId == companyId &&
                df.DevelopmentFormula.CompanyId == companyId)
            .Select(df => df.DevelopmentFormula!.ExternalId)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct()
            .ToListAsync(cancellationToken);

        var model = new ColorChipRecordPdfModel
        {
            BatchNo = data.FirstDevelopmentFormulaCode ?? string.Empty,
            Date = data.RecordDate ?? data.CreatedDate,
            Customer = customerName ?? string.Empty,
            Code = data.ProductColourCode ?? string.Empty,
            Color = data.ProductName ?? string.Empty,
            AddRate = data.ProductUsageRate != null ? $"{data.ProductUsageRate}%" : string.Empty,
            Resin = !string.IsNullOrWhiteSpace(data.Resin) ? data.Resin : data.ResinType.ToString(),
            PreparedBy = !string.IsNullOrWhiteSpace(data.FirstPreparedByName)
                ? data.FirstPreparedByName
                : data.ProductCreatedByName ?? string.Empty,
            Signature = string.Empty,
            Machine = data.Machine,
            TemperatureLimit = data.TemperatureLimit,
            SizeText = data.SizeText,
            PelletWeightGram = data.PelletWeightGram,
            NetWeightGram = data.NetWeightGram,
            Electrostatic = data.Electrostatic,
            Note = data.Note,
            PrintNote = data.PrintNote,
            RecordTypeText = data.RecordType.ToString(),
            ResinTypeText = data.ResinType.ToString(),
            LogoTypeText = data.LogoType.ToString(),
            FormStyleText = data.FormStyle.ToString(),
            DeltaE = data.ProductDeltaE ?? string.Empty,
            DevelopmentFormulaCodes = developmentFormulaCodes
        };

        return OperationResult<ColorChipRecordPdfFileDto>.Ok(new ColorChipRecordPdfFileDto
        {
            FileName = $"ColorChipRecord_{request.ProductId:N}.pdf",
            Content = _renderer.Render(model, data.FormStyle)
        });
    }
}
