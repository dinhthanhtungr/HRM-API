using System.Text.Json;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Features.PLM.Materials.Dtos;
using HRM.Domain.Enums.Materials;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.Materials.Queries.GetMaterialReplacementOptions;

internal sealed class GetMaterialReplacementOptionsQueryHandler
    : IRequestHandler<GetMaterialReplacementOptionsQuery, IReadOnlyList<MaterialReplacementDto>>
{
    private readonly IPLMReadDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetMaterialReplacementOptionsQueryHandler(IPLMReadDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MaterialReplacementDto>> Handle(
        GetMaterialReplacementOptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.SourceMaterialId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty)
        {
            return [];
        }

        var rows = await BaseQuery(companyId, request.SourceMaterialId)
            .Where(x =>
                x.IsActive &&
                x.ReplacementMaterial.IsActive == true &&
                (x.ReplacementMaterial.PurchaseAvailability == null ||
                 x.ReplacementMaterial.PurchaseAvailability.Status != MaterialPurchaseStatus.Unavailable))
            .OrderByDescending(x => x.IsRecommended)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.ReplacementMaterial.Name)
            .Select(x => new ReplacementRow
            {
                MaterialReplacementId = x.MaterialReplacementId,
                SourceMaterialId = x.SourceMaterialId,
                ReplacementMaterialId = x.ReplacementMaterialId,
                ExternalId = x.ReplacementMaterial.ExternalId,
                CustomCode = x.ReplacementMaterial.CustomCode,
                Name = x.ReplacementMaterial.Name,
                PurchaseStatus = x.ReplacementMaterial.PurchaseAvailability == null
                    ? MaterialPurchaseStatus.Available
                    : x.ReplacementMaterial.PurchaseAvailability.Status,
                ApplicableContext = x.ApplicableContext,
                TechnicalNote = x.TechnicalNote,
                ReplacementRatio = x.ReplacementRatio,
                Priority = x.Priority,
                IsRecommended = x.IsRecommended,
                IsActive = x.IsActive,
                CreatedDate = x.CreatedDate,
                UpdatedDate = x.UpdatedDate
            })
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    private IQueryable<HRM.Domain.Entities.MaterialSchema.MaterialReplacement> BaseQuery(Guid companyId, Guid sourceMaterialId)
        => _dbContext.MaterialReplacements
            .AsNoTracking()
            .Where(x =>
                x.SourceMaterialId == sourceMaterialId &&
                x.SourceMaterial.CompanyId == companyId &&
                x.SourceMaterial.IsActive == true &&
                x.ReplacementMaterial.CompanyId == companyId);

    internal static MaterialReplacementDto ToDto(ReplacementRow row)
        => new()
        {
            MaterialReplacementId = row.MaterialReplacementId,
            SourceMaterialId = row.SourceMaterialId,
            ReplacementMaterialId = row.ReplacementMaterialId,
            ExternalId = row.ExternalId,
            CustomCode = row.CustomCode,
            Name = row.Name,
            PurchaseStatus = row.PurchaseStatus,
            IsPurchaseAvailable = row.PurchaseStatus != MaterialPurchaseStatus.Unavailable,
            ApplicableContext = row.ApplicableContext.RootElement.Clone(),
            TechnicalNote = row.TechnicalNote,
            ReplacementRatio = row.ReplacementRatio,
            Priority = row.Priority,
            IsRecommended = row.IsRecommended,
            IsActive = row.IsActive,
            CreatedDate = row.CreatedDate,
            UpdatedDate = row.UpdatedDate
        };

    internal sealed class ReplacementRow
    {
        public Guid MaterialReplacementId { get; init; }
        public Guid SourceMaterialId { get; init; }
        public Guid ReplacementMaterialId { get; init; }
        public string? ExternalId { get; init; }
        public string? CustomCode { get; init; }
        public string? Name { get; init; }
        public MaterialPurchaseStatus PurchaseStatus { get; init; }
        public JsonDocument ApplicableContext { get; init; } = JsonDocument.Parse("{}");
        public string? TechnicalNote { get; init; }
        public decimal? ReplacementRatio { get; init; }
        public int Priority { get; init; }
        public bool IsRecommended { get; init; }
        public bool IsActive { get; init; }
        public DateTime CreatedDate { get; init; }
        public DateTime? UpdatedDate { get; init; }
    }
}
