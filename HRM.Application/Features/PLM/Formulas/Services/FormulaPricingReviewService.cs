using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.Formulas.Dtos.Commons;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.PriceQuoteRequests;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;
using HRM.Domain.Enums.Formulas;
using HRM.Domain.Enums.Notifications;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HRM.Domain.Enums.Products;

namespace HRM.Application.Features.PLM.Formulas.Services;

/// <summary>
/// Dùng chung luồng yêu cầu xác nhận giá chuẩn khi Lab xác nhận hoặc thay đổi NVL đã có giá chuẩn.
/// </summary>
internal sealed class FormulaPricingReviewService
{
    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICRMReadDbContext _crm;
    private readonly ISender _sender;

    public FormulaPricingReviewService(IPLMWriteDbContext dbContext, ICRMReadDbContext crm, ISender sender)
    {
        _dbContext = dbContext;
        _crm = crm;
        _sender = sender;
    }

    public async Task<bool> RequiresMaterialReviewAsync(
        Formula formula,
        UpsertFormulaRequest? request,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(formula.Status, FormulaStatus.Approved.ToString(), StringComparison.OrdinalIgnoreCase) ||
            request?.Materials is null ||
            (request.ProductId is { } productId && productId != Guid.Empty && productId != formula.ProductId))
        {
            return false;
        }

        var current = await _dbContext.FormulaMaterials.AsNoTracking()
            .Where(x => x.FormulaId == formula.FormulaId && x.Formula.CompanyId == companyId && x.IsActive)
            .Select(x => new FormulaMaterialCompositionItem(x.itemType, x.MaterialId ?? x.ProductId ?? Guid.Empty, x.Quantity))
            .ToListAsync(cancellationToken);
        if (!FormulaPricingReviewRules.HasMaterialChanges(current, request.Materials))
        {
            return false;
        }

        var approvedSource = await _crm.ProductPricingVersions.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ProductId == formula.ProductId &&
                x.IsActive && x.Currency == "VND" && x.Status == ProductPricingStatus.Approved && x.StandardSellingPrice > 0m)
            .OrderByDescending(x => x.Version)
            .ThenByDescending(x => x.ApprovedAt ?? x.UpdatedDate ?? x.CreatedDate)
            .Select(x => x.SourceFormulaId)
            .FirstOrDefaultAsync(cancellationToken);
        return approvedSource == formula.FormulaId;
    }

    public async Task<OperationResult<SendInternalMessageResultDto>> SendMaterialChangeAsync(
        Formula formula, Guid companyId, Guid? sampleRequestId, CancellationToken cancellationToken)
    {
        var targets = _dbContext.SampleRequests.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ProductId == formula.ProductId && x.IsActive);
        var targetId = sampleRequestId ?? await targets
            .OrderByDescending(x => x.FormulaId == formula.FormulaId)
            .ThenByDescending(x => x.CreatedDate)
            .ThenByDescending(x => x.SampleRequestId)
            .Select(x => (Guid?)x.SampleRequestId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!targetId.HasValue)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("No active sample request was found for the formula product.");
        }

        return await SendAsync(targetId.Value, formula.ProductId, companyId, formula.FormulaId,
            formula.ExternalId, formula.Name, materialsChanged: true, cancellationToken);
    }
    public async Task<OperationResult<SendInternalMessageResultDto>> SendAsync(
        Guid sampleRequestId,
        Guid formulaProductId,
        Guid companyId,
        Guid formulaId,
        string formulaExternalId,
        string formulaName,
        bool materialsChanged,
        CancellationToken cancellationToken)
    {
        var sampleRequest = await _dbContext.SampleRequests
            .AsNoTracking()
            .Where(x =>
                x.SampleRequestId == sampleRequestId &&
                x.CompanyId == companyId &&
                x.ProductId == formulaProductId &&
                x.IsActive)
            .Select(x => new
            {
                x.SampleRequestId,
                x.ExternalId,
                x.ProductId,
                ProductCode = x.Product.ColourCode ?? x.Product.Code ?? string.Empty,
                ProductName = x.Product.Name ?? string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sampleRequest is null)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(
                "Sample request was not found or does not belong to the formula product.");
        }

        var presidentEmployeeIds = await _dbContext.Employees.AsNoTracking()
            .Where(employee =>
                employee.CompanyId == companyId &&
                employee.IsActive &&
                employee.ApplicationUsers.Any(user => user.UserRoles.Any(role =>
                    role.IsActive && role.Role.Name == ApplicationRoles.President)))
            .Select(employee => employee.EmployeeId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (presidentEmployeeIds.Count == 0)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(
                "No active President employee was found in the current company.");
        }

        var pricingLink = "/executive/sample-request-pricing-overview?productId=" +
            Uri.EscapeDataString(sampleRequest.ProductId.ToString()) +
            "&sampleRequestId=" + Uri.EscapeDataString(sampleRequest.SampleRequestId.ToString()) +
            "&sourceType=VU&sourceId=" + Uri.EscapeDataString(formulaId.ToString());

        var payload = new SampleRequestPriceQuotePayload
        {
            SampleRequestId = sampleRequest.SampleRequestId,
            SampleRequestExternalId = sampleRequest.ExternalId,
            ProductId = sampleRequest.ProductId,
            ProductCode = sampleRequest.ProductCode,
            ProductName = sampleRequest.ProductName,
            FormulaId = formulaId,
            FormulaExternalId = formulaExternalId,
            FormulaName = formulaName,
            FormulaSelectionSource = "LabConfirmed",
            Action = new SampleRequestPriceQuoteActionDto
            {
                Code = "Executive.OpenProductPricingReview",
                Parameters = new SampleRequestPriceQuoteActionParametersDto
                {
                    SampleRequestId = sampleRequest.SampleRequestId,
                    SampleRequestExternalId = sampleRequest.ExternalId,
                    ProductId = sampleRequest.ProductId,
                    ProductCode = sampleRequest.ProductCode,
                    FormulaId = formulaId
                }
            }
        };

        return await _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = sampleRequest.SampleRequestId,
            Type = SampleRequestNotificationType.PriceQuoteRequest,
            Message = materialsChanged
                ? $"Lab đã thay đổi NVL của công thức [{formulaExternalId}] - {formulaName} cho yêu cầu phối mẫu {sampleRequest.ExternalId}. Giá chuẩn của sản phẩm cần được Ban Giám đốc xác nhận lại."
                : $"Lab đã xác nhận công thức [{formulaExternalId}] - {formulaName} cho yêu cầu phối mẫu {sampleRequest.ExternalId}. Giá chuẩn của sản phẩm cần được Ban Giám đốc xác nhận lại.",
            ExtraRecipientEmployeeIds = presidentEmployeeIds,
            TopicOverride = TopicNotifications.SampleRequestPriceQuoteRequested,
            TitleOverride = "Yêu cầu xác nhận lại giá chuẩn",
            LinkOverride = pricingLink,
            PriceQuoteRequest = payload
        }, cancellationToken);
    }
}
