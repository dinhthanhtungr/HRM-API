using HRM.Application.Abstractions.Persistence.PLM;
using HRM.Domain.Enums.InternalMailEnums;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.PLM.SampleRequests.Services;

public sealed class SampleRequestConversationSubjectService
{
    private readonly IPLMWriteDbContext _dbContext;

    public SampleRequestConversationSubjectService(IPLMWriteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public static string BuildSubject(string sampleRequestExternalId, string? colourCode)
    {
        var baseSubject = $"{sampleRequestExternalId}";
        var normalizedColourCode = string.IsNullOrWhiteSpace(colourCode)
            ? null
            : colourCode.Trim();

        return normalizedColourCode is null
            ? baseSubject
            : $"{baseSubject} - {normalizedColourCode}";
    }

    public async Task SyncSubjectAsync(
        Guid sampleRequestId,
        Guid companyId,
        string sampleRequestExternalId,
        string? colourCode,
        CancellationToken cancellationToken)
    {
        if (sampleRequestId == Guid.Empty ||
            companyId == Guid.Empty ||
            string.IsNullOrWhiteSpace(sampleRequestExternalId))
        {
            return;
        }

        var conversation = await _dbContext.InternalConversations
            .FirstOrDefaultAsync(x =>
                x.CompanyId == companyId &&
                x.IsActive &&
                x.RelatedType == InternalMailRelatedType.SampleRequest &&
                x.RelatedId == sampleRequestId,
                cancellationToken);

        if (conversation is null)
        {
            return;
        }

        var subject = BuildSubject(sampleRequestExternalId, colourCode);
        if (!string.Equals(conversation.Subject, subject, StringComparison.Ordinal))
        {
            conversation.Subject = subject;
        }
    }
}
