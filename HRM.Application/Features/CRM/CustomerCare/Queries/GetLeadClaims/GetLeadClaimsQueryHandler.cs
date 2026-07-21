using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetLeadClaims;

internal sealed class GetLeadClaimsQueryHandler
    : IRequestHandler<GetLeadClaimsQuery, OperationResult<LeadClaimDetailsDto>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetLeadClaimsQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<OperationResult<LeadClaimDetailsDto>> Handle(
        GetLeadClaimsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return OperationResult<LeadClaimDetailsDto>.Fail("CustomerId is invalid.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var customer = await _visibilityService
            .ApplyCustomerVisibility(_dbContext.Customers.AsNoTracking(), scope)
            .Where(item => item.CustomerId == request.CustomerId)
            .Select(item => new
            {
                item.CustomerId,
                item.ExternalId,
                item.CustomerName,
                item.IsLead
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return OperationResult<LeadClaimDetailsDto>.Fail("Lead was not found or is outside your visibility scope.");
        }

        if (!customer.IsLead)
        {
            return OperationResult<LeadClaimDetailsDto>.Fail("Customer is not a lead.");
        }

        var now = scope.Now;
        var claimRows = await _dbContext.CustomerClaims
            .AsNoTracking()
            .Where(claim =>
                claim.CustomerId == request.CustomerId &&
                claim.CompanyId == scope.CompanyId &&
                claim.IsActive &&
                claim.Type == ClaimType.Work &&
                claim.ExpiresAt > now)
            .OrderByDescending(claim => claim.ExpiresAt)
            .Select(claim => new
            {
                ClaimId = claim.Id,
                claim.EmployeeId,
                EmployeeName = claim.Employee.FullName,
                claim.GroupId,
                GroupName = claim.Group.Name,
                claim.ExpiresAt
            })
            .ToListAsync(cancellationToken);

        var claimEmployeeIds = claimRows
            .Select(claim => claim.EmployeeId)
            .Distinct()
            .ToArray();

        var interactionRows = claimEmployeeIds.Length == 0
            ? new List<InteractionRow>()
            : await _dbContext.CustomerInteractions
                .AsNoTracking()
                .Where(interaction =>
                    interaction.CompanyId == scope.CompanyId &&
                    interaction.CustomerId == request.CustomerId &&
                    interaction.IsActive &&
                    (
                        interaction.AssignedSaleEmployeeId.HasValue &&
                        claimEmployeeIds.Contains(interaction.AssignedSaleEmployeeId.Value) ||
                        claimEmployeeIds.Contains(interaction.CreatedBy)
                    ))
                .OrderByDescending(interaction => interaction.InteractionAt)
                .Select(interaction => new InteractionRow(
                    interaction.Id,
                    interaction.AssignedSaleEmployeeId,
                    interaction.CreatedBy,
                    interaction.InteractionType,
                    interaction.Subject,
                    interaction.Content,
                    interaction.Outcome,
                    interaction.NextAction,
                    interaction.InteractionAt,
                    interaction.NextFollowUpDate))
                .ToListAsync(cancellationToken);

        var interactionLimit = request.NormalizedInteractionLimit;
        var claims = claimRows
            .Select(claim =>
            {
                var remaining = claim.ExpiresAt - now;
                var interactions = interactionRows
                    .Where(interaction =>
                        interaction.AssignedSaleEmployeeId == claim.EmployeeId ||
                        interaction.CreatedBy == claim.EmployeeId)
                    .OrderByDescending(interaction => interaction.InteractionAt)
                    .Take(interactionLimit)
                    .Select(interaction => new LeadClaimInteractionDto
                    {
                        InteractionId = interaction.InteractionId,
                        InteractionType = interaction.InteractionType.ToString(),
                        Subject = interaction.Subject,
                        Content = interaction.Content,
                        Outcome = interaction.Outcome,
                        NextAction = interaction.NextAction,
                        InteractionAt = interaction.InteractionAt,
                        NextFollowUpDate = interaction.NextFollowUpDate
                    })
                    .ToList();

                return new LeadClaimOwnerDto
                {
                    ClaimId = claim.ClaimId,
                    EmployeeId = claim.EmployeeId,
                    EmployeeName = claim.EmployeeName,
                    GroupId = claim.GroupId,
                    GroupName = claim.GroupName,
                    ExpiresAt = claim.ExpiresAt,
                    RemainingHours = Math.Max(0, Math.Round(remaining.TotalHours, 2)),
                    RemainingDays = Math.Max(0, (int)Math.Ceiling(remaining.TotalDays)),
                    Interactions = interactions
                };
            })
            .ToList();

        return OperationResult<LeadClaimDetailsDto>.Ok(new LeadClaimDetailsDto
        {
            CustomerId = customer.CustomerId,
            CustomerName = customer.CustomerName,
            ExternalId = customer.ExternalId,
            Claims = claims
        });
    }

    private sealed record InteractionRow(
        Guid InteractionId,
        Guid? AssignedSaleEmployeeId,
        Guid CreatedBy,
        CustomerInteractionType InteractionType,
        string? Subject,
        string Content,
        string? Outcome,
        string? NextAction,
        DateTime InteractionAt,
        DateTime? NextFollowUpDate);
}
