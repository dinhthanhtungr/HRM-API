using HRM.Application.Abstractions.Persistence.InternalMail;
using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Application.Abstractions.Security;
using HRM.Application.Commons.Models;
using HRM.Application.Features.MessageRecipients.Dtos;
using HRM.Application.Features.MessageRecipients.Queries.PreviewMessageRecipients;
using HRM.Application.Features.MessageRecipients.Services;
using HRM.Application.Features.PLM.SampleRequests.Rules;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

internal sealed class SampleRequestMessageRecipientResolver : IMessageRecipientResolver
{
    private const string ContextType = "SampleRequest";

    private readonly IPLMReadDbContext _plmDbContext;
    private readonly IInternalMailDbContext _internalMailDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly SampleRequestRecipientResolver _sampleRequestRecipientResolver;

    public SampleRequestMessageRecipientResolver(
        IPLMReadDbContext plmDbContext,
        IInternalMailDbContext internalMailDbContext,
        ICurrentUser currentUser,
        SampleRequestRecipientResolver sampleRequestRecipientResolver)
    {
        _plmDbContext = plmDbContext;
        _internalMailDbContext = internalMailDbContext;
        _currentUser = currentUser;
        _sampleRequestRecipientResolver = sampleRequestRecipientResolver;
    }

    public bool CanResolve(string contextType)
        => string.Equals(contextType, ContextType, StringComparison.OrdinalIgnoreCase);

    public async Task<OperationResult<MessageRecipientPreviewDto>> PreviewAsync(
        PreviewMessageRecipientsQuery request,
        CancellationToken cancellationToken)
    {
        var companyId = _currentUser.CompanyId;
        var currentEmployeeId = _currentUser.EmployeeId;
        if (!companyId.HasValue || companyId.Value == Guid.Empty ||
            !currentEmployeeId.HasValue || currentEmployeeId.Value == Guid.Empty)
        {
            return OperationResult<MessageRecipientPreviewDto>.Fail("Current user is invalid.");
        }

        var suggestedIds = new HashSet<Guid>();
        var suppressRecipients = false;
        string? productCategoryExternalId = null;
        if (request.ContextId is { } sampleRequestId && sampleRequestId != Guid.Empty)
        {
            var sampleRequest = await _plmDbContext.SampleRequests
                .AsNoTracking()
                .Where(x =>
                    x.SampleRequestId == sampleRequestId &&
                    x.CompanyId == companyId.Value &&
                    x.IsActive)
                .Select(x => new
                {
                    x.CreatedBy,
                    x.ManagerBy,
                    x.RequestType,
                    CustomerExternalId = x.Customer.ExternalId,
                    CategoryExternalId = x.Product.Category != null ? x.Product.Category.ExternalId : null
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (sampleRequest is null)
            {
                return OperationResult<MessageRecipientPreviewDto>.Fail("Sample request was not found.");
            }

            suppressRecipients = SampleRequestMessageRules.ShouldSuppressMessages(
                sampleRequest.RequestType,
                sampleRequest.CustomerExternalId);
            if (suppressRecipients)
            {
                return OperationResult<MessageRecipientPreviewDto>.Ok(new MessageRecipientPreviewDto
                {
                    ContextType = ContextType,
                    ActionType = request.ActionType.Trim(),
                    RequiredRecipients = Array.Empty<MessageRecipientDto>(),
                    SuggestedRecipients = Array.Empty<MessageRecipientDto>(),
                    SelectedRecipients = Array.Empty<MessageRecipientDto>(),
                    CanAddRecipients = false,
                    CanRemoveSuggestedRecipients = true
                });
            }

            productCategoryExternalId = sampleRequest.CategoryExternalId;
            suggestedIds.Add(sampleRequest.CreatedBy);
            suggestedIds.Add(sampleRequest.ManagerBy);

            var participantIds = await _internalMailDbContext.InternalConversationParticipants
                .AsNoTracking()
                .Where(x =>
                    x.IsActive &&
                    x.Conversation.CompanyId == companyId.Value &&
                    x.Conversation.IsActive &&
                    x.Conversation.RelatedType == InternalMailRelatedType.SampleRequest &&
                    x.Conversation.RelatedId == sampleRequestId)
                .Select(x => x.EmployeeId)
                .ToListAsync(cancellationToken);

            foreach (var participantId in participantIds)
            {
                suggestedIds.Add(participantId);
            }
        }
        else if (request.DraftManagerBy is { } draftManagerBy && draftManagerBy != Guid.Empty)
        {
            suggestedIds.Add(draftManagerBy);
        }

        if (productCategoryExternalId is null &&
            request.DraftCategoryId is { } draftCategoryId &&
            draftCategoryId != Guid.Empty)
        {
            productCategoryExternalId = await _plmDbContext.Categories
                .AsNoTracking()
                .Where(x =>
                    x.CategoryId == draftCategoryId &&
                    x.CompanyId == companyId.Value &&
                    x.IsActive == true &&
                    x.Types == "Product")
                .Select(x => x.ExternalId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var defaultRecipients = await _sampleRequestRecipientResolver.ResolveDefaultMessageRecipientsAsync(
                companyId.Value,
                productCategoryExternalId,
                cancellationToken);

        var requiredRecipients = defaultRecipients
            .Where(x => x.Locked)
            .Select(ToMessageRecipientDto)
            .ToList();

        var defaultOptionalRecipients = defaultRecipients
            .Where(x => !x.Locked)
            .Select(ToMessageRecipientDto)
            .ToList();

        suggestedIds.Add(currentEmployeeId.Value);

        var requiredIds = requiredRecipients
            .Select(x => x.EmployeeId)
            .ToHashSet();

        var suggestedRecipients = await ResolveEmployeesAsync(
            suggestedIds.Where(x => x != Guid.Empty && !requiredIds.Contains(x)).ToArray(),
            companyId.Value,
            "suggested",
            "Sample request participant",
            locked: false,
            cancellationToken);

        suggestedRecipients = defaultOptionalRecipients
            .Concat(suggestedRecipients.Where(x => !requiredIds.Contains(x.EmployeeId)))
            .GroupBy(x => x.EmployeeId)
            .Select(x => x.First())
            .OrderBy(x => x.FullName)
            .ToList();

        var selectedIds = request.SelectedRecipientEmployeeIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        var selectedOptionalIds = selectedIds ?? defaultOptionalRecipients
            .Select(x => x.EmployeeId)
            .ToArray();

        var selectedRecipients = await ResolveEmployeesAsync(
            requiredIds.Concat(selectedOptionalIds).Distinct().ToArray(),
            companyId.Value,
            "selected",
            "Selected recipient",
            locked: false,
            cancellationToken);

        var selectedFoundIds = selectedRecipients
            .Select(x => x.EmployeeId)
            .ToHashSet();

        if (selectedOptionalIds.Any(x => !selectedFoundIds.Contains(x)))
        {
            return OperationResult<MessageRecipientPreviewDto>.Fail("Some selected recipients do not exist or are inactive.");
        }

        foreach (var selectedRecipient in selectedRecipients)
        {
            var defaultRecipient = defaultRecipients.FirstOrDefault(x => x.EmployeeId == selectedRecipient.EmployeeId);
            if (defaultRecipient is null)
            {
                continue;
            }

            selectedRecipient.Source = defaultRecipient.Source;
            selectedRecipient.Reason = defaultRecipient.Reason;
            selectedRecipient.Locked = defaultRecipient.Locked;
        }

        return OperationResult<MessageRecipientPreviewDto>.Ok(new MessageRecipientPreviewDto
        {
            ContextType = ContextType,
            ActionType = request.ActionType.Trim(),
            RequiredRecipients = requiredRecipients,
            SuggestedRecipients = suggestedRecipients,
            SelectedRecipients = selectedRecipients,
            CanAddRecipients = true,
            CanRemoveSuggestedRecipients = true
        });
    }

    private async Task<IReadOnlyList<MessageRecipientDto>> ResolveEmployeesAsync(
        IReadOnlyCollection<Guid> employeeIds,
        Guid companyId,
        string source,
        string reason,
        bool locked,
        CancellationToken cancellationToken)
    {
        if (employeeIds.Count == 0)
        {
            return Array.Empty<MessageRecipientDto>();
        }

        return await _internalMailDbContext.Employees
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                employeeIds.Contains(x.EmployeeId))
            .OrderBy(x => x.FullName)
            .Select(x => new MessageRecipientDto
            {
                EmployeeId = x.EmployeeId,
                FullName = x.FullName,
                ExternalId = x.ExternalId,
                Source = source,
                Reason = reason,
                Locked = locked
            })
            .ToListAsync(cancellationToken);
    }

    private static MessageRecipientDto ToMessageRecipientDto(SampleRequestRecipientDto recipient)
    {
        return new MessageRecipientDto
        {
            EmployeeId = recipient.EmployeeId,
            FullName = recipient.FullName,
            ExternalId = recipient.ExternalId,
            Source = recipient.Source,
            Reason = recipient.Reason,
            Locked = recipient.Locked
        };
    }
}
