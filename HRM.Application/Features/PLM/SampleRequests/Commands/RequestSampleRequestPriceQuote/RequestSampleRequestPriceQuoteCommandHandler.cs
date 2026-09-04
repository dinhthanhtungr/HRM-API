using System.Text;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Authorization;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Application.Features.InternalMail.Dtos;
using HRM.Application.Features.PLM.SampleRequests.Commands.SendSampleRequestMessage;
using HRM.Application.Features.PLM.SampleRequests.PriceQuoteRequests;
using HRM.Domain.Enums.SampleRequests;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Commands.RequestSampleRequestPriceQuote;

/// <summary>
/// Creates a price quote request in the existing Sample Request conversation.
/// An explicit Formula wins; otherwise the latest active trial Formula and the
/// Sample Request Formula are considered in that order.
/// </summary>
internal sealed class RequestSampleRequestPriceQuoteCommandHandler
    : IRequestHandler<RequestSampleRequestPriceQuoteCommand, OperationResult<SendInternalMessageResultDto>>
{
    private const int MaxUserMessageLength = 1000;

    private readonly IPLMWriteDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ICustomerVisibilityService _visibilityService;
    private readonly ISender _sender;

    public RequestSampleRequestPriceQuoteCommandHandler(
        IPLMWriteDbContext dbContext,
        ICurrentUser currentUser,
        ICustomerVisibilityService visibilityService,
        ISender sender)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _visibilityService = visibilityService;
        _sender = sender;
    }

    public async Task<OperationResult<SendInternalMessageResultDto>> Handle(
        RequestSampleRequestPriceQuoteCommand command,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsInAnyRole(ApplicationRoleSets.PLM.SampleRequestPriceQuoteRequesters))
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(
                "You are not allowed to request a price quote for this Sample Request.");
        }

        if (command.SampleRequestId == Guid.Empty ||
            _currentUser.CompanyId is not { } companyId || companyId == Guid.Empty ||
            _currentUser.EmployeeId is not { } employeeId || employeeId == Guid.Empty)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(
                "Current user or Sample Request is invalid.");
        }

        if (command.Request.FormulaId == Guid.Empty)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail("FormulaId is invalid.");
        }

        var userMessage = command.Request.Message?.Trim();
        if (userMessage?.Length > MaxUserMessageLength)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(
                $"Message cannot exceed {MaxUserMessageLength} characters.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var visibleSampleRequests = _visibilityService.ApplySampleRequestVisibility(
            _dbContext.SampleRequests.AsNoTracking(),
            _dbContext.Customers.AsNoTracking(),
            scope);

        var sampleRequest = await visibleSampleRequests
            .Where(x =>
                x.SampleRequestId == command.SampleRequestId &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new
            {
                x.SampleRequestId,
                x.ExternalId,
                x.ProductId,
                x.FormulaId,
                x.Status,
                ProductCode = x.Product.ColourCode ?? x.Product.Code ?? string.Empty,
                ProductName = x.Product.Name ?? string.Empty,
                CustomerExternalId = x.Customer.ExternalId,
                CustomerName = x.Customer.CustomerName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sampleRequest is null)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(
                "Sample Request was not found or is outside your visibility scope.");
        }

        if (string.Equals(sampleRequest.Status, nameof(SampleRequestStatus.New), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sampleRequest.Status, nameof(SampleRequestStatus.Cancelled), StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(
                "A price quote can only be requested after the Sample Request starts processing.");
        }

        var selectedFormula = await ResolveFormulaAsync(
            sampleRequest.SampleRequestId,
            sampleRequest.ProductId,
            sampleRequest.FormulaId,
            command.Request.FormulaId,
            companyId,
            cancellationToken);

        if (command.Request.FormulaId.HasValue && selectedFormula is null)
        {
            return OperationResult<SendInternalMessageResultDto>.Fail(
                "The selected Formula is inactive, inaccessible, or does not belong to this product.");
        }

        var presidentEmployeeIds = await _dbContext.Employees
            .AsNoTracking()
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

        var payload = new SampleRequestPriceQuotePayload
        {
            SampleRequestId = sampleRequest.SampleRequestId,
            SampleRequestExternalId = sampleRequest.ExternalId,
            ProductId = sampleRequest.ProductId,
            ProductCode = sampleRequest.ProductCode,
            ProductName = sampleRequest.ProductName,
            FormulaId = selectedFormula?.FormulaId,
            FormulaExternalId = selectedFormula?.ExternalId,
            FormulaName = selectedFormula?.Name,
            FormulaSelectionSource = selectedFormula?.SelectionSource ?? "ProductOnly",
            Action = new SampleRequestPriceQuoteActionDto
            {
                Parameters = new SampleRequestPriceQuoteActionParametersDto
                {
                    SampleRequestId = sampleRequest.SampleRequestId,
                    SampleRequestExternalId = sampleRequest.ExternalId,
                    ProductId = sampleRequest.ProductId,
                    ProductCode = sampleRequest.ProductCode,
                    FormulaId = selectedFormula?.FormulaId
                }
            }
        };

        var pricingLink = string.IsNullOrWhiteSpace(sampleRequest.ProductCode)
            ? "/crm/quotations/product-pricing-options"
            : "/crm/quotations/product-pricing-options?keyword=" +
              Uri.EscapeDataString(sampleRequest.ProductCode.Trim());

        return await _sender.Send(new SendSampleRequestMessageCommand
        {
            SampleRequestId = sampleRequest.SampleRequestId,
            Type = SampleRequestNotificationType.PriceQuoteRequest,
            Message = BuildMessage(
                sampleRequest.ExternalId,
                sampleRequest.ProductCode,
                sampleRequest.ProductName,
                sampleRequest.CustomerExternalId,
                sampleRequest.CustomerName,
                selectedFormula,
                userMessage),
            IsUrgent = command.Request.IsUrgent,
            ExtraRecipientEmployeeIds = presidentEmployeeIds,
            TitleOverride = "Yêu cầu báo giá sản phẩm",
            LinkOverride = pricingLink,
            NotificationRecipientEmployeeIdsOverride = presidentEmployeeIds
                .Where(id => id != employeeId)
                .ToArray(),
            PriceQuoteRequest = payload
        }, cancellationToken);
    }

    private async Task<SelectedFormula?> ResolveFormulaAsync(
        Guid sampleRequestId,
        Guid productId,
        Guid? sampleRequestFormulaId,
        Guid? requestedFormulaId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (requestedFormulaId.HasValue)
        {
            return await LoadFormulaAsync(
                requestedFormulaId.Value,
                productId,
                companyId,
                "Explicit",
                cancellationToken);
        }

        var latestTrialFormulaId = await _dbContext.SampleRequestSampleTrials
            .AsNoTracking()
            .Where(x =>
                x.SampleRequestId == sampleRequestId &&
                x.IsActive &&
                x.FormulaId.HasValue)
            .OrderByDescending(x => x.TrialNo)
            .ThenByDescending(x => x.UpdatedDate ?? x.CreatedDate)
            .Select(x => x.FormulaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestTrialFormulaId.HasValue)
        {
            var latestTrialFormula = await LoadFormulaAsync(
                latestTrialFormulaId.Value,
                productId,
                companyId,
                "LatestSampleTrial",
                cancellationToken);
            if (latestTrialFormula is not null)
            {
                return latestTrialFormula;
            }
        }

        if (sampleRequestFormulaId.HasValue && sampleRequestFormulaId != latestTrialFormulaId)
        {
            return await LoadFormulaAsync(
                sampleRequestFormulaId.Value,
                productId,
                companyId,
                "SampleRequest",
                cancellationToken);
        }

        return null;
    }

    private async Task<SelectedFormula?> LoadFormulaAsync(
        Guid formulaId,
        Guid productId,
        Guid companyId,
        string selectionSource,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Formulas
            .AsNoTracking()
            .Where(x =>
                x.FormulaId == formulaId &&
                x.ProductId == productId &&
                x.CompanyId == companyId &&
                x.IsActive)
            .Select(x => new SelectedFormula(
                x.FormulaId,
                x.ExternalId,
                x.Name,
                selectionSource))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string BuildMessage(
        string sampleRequestExternalId,
        string productCode,
        string productName,
        string? customerExternalId,
        string? customerName,
        SelectedFormula? formula,
        string? userMessage)
    {
        var builder = new StringBuilder()
            .Append("Yêu cầu báo giá ").AppendLine(sampleRequestExternalId)
            .Append("Sản phẩm: [").Append(productCode).Append("] ").AppendLine(productName)
            .Append("Khách hàng: [").Append(customerExternalId).Append("] ").AppendLine(customerName)
            .Append("Công thức: ");

        if (formula is null)
        {
            builder.AppendLine("Chưa xác định");
        }
        else
        {
            builder.Append('[').Append(formula.ExternalId).Append("] - ").AppendLine(formula.Name);
        }

        if (!string.IsNullOrWhiteSpace(userMessage))
        {
            builder.Append("Nội dung: ").Append(userMessage);
        }

        return builder.ToString().TrimEnd();
    }

    private sealed record SelectedFormula(
        Guid FormulaId,
        string ExternalId,
        string Name,
        string SelectionSource);
}
