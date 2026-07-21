using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using HRM.Application.Commons.Models;
using HRM.Application.Features.CRM.CustomerCare.Dtos;
using HRM.Application.Features.CRM.CustomerCare.Visibility;
using HRM.Domain.Enums.CustomerEnum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Features.CRM.CustomerCare.Queries.GetCustomerById;

internal sealed class GetCustomerByIdQueryHandler
    : IRequestHandler<GetCustomerByIdQuery, OperationResult<CustomerDetailDto>>
{
    private readonly ICRMReadDbContext _dbContext;
    private readonly ICustomerVisibilityService _visibilityService;

    public GetCustomerByIdQueryHandler(
        ICRMReadDbContext dbContext,
        ICustomerVisibilityService visibilityService)
    {
        _dbContext = dbContext;
        _visibilityService = visibilityService;
    }

    public async Task<OperationResult<CustomerDetailDto>> Handle(
        GetCustomerByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return OperationResult<CustomerDetailDto>.Fail("CustomerId is invalid.");
        }

        var scope = await _visibilityService.BuildScopeAsync(cancellationToken);
        var leaderGroupIds = scope.LeaderGroupIds.ToArray();
        var restrictAssignmentsToLeaderGroups = !scope.HasFullCustomerView && leaderGroupIds.Length > 0;

        var viewerGroupIds = await _dbContext.MemberInGroups
            .AsNoTracking()
            .Where(member =>
                member.Profile == scope.EmployeeId &&
                member.IsActive &&
                member.Group.CompanyId == scope.CompanyId)
            .Select(member => member.GroupId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var detail = await _visibilityService
            .ApplyCustomerVisibility(_dbContext.Customers.AsNoTracking(), scope)
            .Where(customer => customer.CustomerId == request.CustomerId)
            .Select(customer => new
            {
                Customer = customer,
                HasActiveAssignment = customer.CustomerAssignments.Any(assignment => assignment.IsActive),
                LatestAssignment = customer.CustomerAssignments
                    .Where(assignment =>
                        assignment.IsActive &&
                        (!restrictAssignmentsToLeaderGroups || leaderGroupIds.Contains(assignment.GroupId)))
                    .OrderByDescending(assignment => assignment.CreatedDate)
                    .Select(assignment => new
                    {
                        EmployeeId = (Guid?)assignment.EmployeeId,
                        EmployeeName = assignment.Employee.FullName
                    })
                    .FirstOrDefault(),
                LatestClaim = customer.CustomerClaims
                    .Where(claim =>
                        claim.IsActive &&
                        claim.Type == ClaimType.Work &&
                        claim.ExpiresAt > scope.Now)
                    .OrderByDescending(claim => claim.ExpiresAt)
                    .Select(claim => new
                    {
                        EmployeeId = (Guid?)claim.EmployeeId,
                        EmployeeName = claim.Employee.FullName
                    })
                    .FirstOrDefault(),
                StoredCurrentSaleName = _dbContext.Employees
                    .Where(employee =>
                        customer.CurrentSaleId.HasValue &&
                        employee.EmployeeId == customer.CurrentSaleId.Value &&
                        employee.CompanyId == scope.CompanyId)
                    .Select(employee => employee.FullName)
                    .FirstOrDefault()
            })
            .Select(item => new CustomerDetailDto
            {
                CustomerId = item.Customer.CustomerId,
                ExternalId = item.Customer.ExternalId,
                CustomerName = item.Customer.CustomerName,
                CustomerGroup = item.Customer.CustomerGroup,
                ApplicationName = item.Customer.ApplicationName,
                RegistrationNumber = item.Customer.RegistrationNumber,
                RegistrationAddress = item.Customer.RegistrationAddress,
                TaxNumber = item.Customer.TaxNumber,
                Phone = item.Customer.Phone,
                Website = item.Customer.Website,
                IssueDate = item.Customer.IssueDate,
                IssuedPlace = item.Customer.IssuedPlace,
                FaxNumber = item.Customer.FaxNumber,
                CompanyName = item.Customer.Company != null ? item.Customer.Company.Name : null,
                IsLead = item.Customer.IsLead,
                LeadStatus = item.Customer.LeadStatus.ToString(),
                CurrentCrmStatus = item.Customer.CurrentCrmStatus,
                CurrentSaleId = item.Customer.IsLead && !item.HasActiveAssignment
                    ? item.LatestClaim != null ? item.LatestClaim.EmployeeId : item.Customer.CurrentSaleId
                    : item.LatestAssignment != null ? item.LatestAssignment.EmployeeId : item.Customer.CurrentSaleId,
                CurrentSaleName = item.Customer.IsLead && !item.HasActiveAssignment
                    ? item.LatestClaim != null ? item.LatestClaim.EmployeeName : item.StoredCurrentSaleName
                    : item.LatestAssignment != null ? item.LatestAssignment.EmployeeName : item.StoredCurrentSaleName,
                LastContactDate = item.Customer.LastContactDate,
                NextFollowUpDate = item.Customer.NextFollowUpDate,
                CreatedDate = item.Customer.CreatedDate,
                IsActive = item.Customer.IsActive,
                Addresses = item.Customer.Addresses
                    .OrderByDescending(address => address.IsActive)
                    .ThenByDescending(address => address.IsPrimary == true)
                    .ThenBy(address => address.AddressId)
                    .Select(address => new CustomerAddressDto
                    {
                        AddressId = address.AddressId,
                        AddressLine = address.AddressLine,
                        City = address.City,
                        District = address.District,
                        Province = address.Province,
                        Country = address.Country,
                        PostalCode = address.PostalCode,
                        IsPrimary = address.IsPrimary,
                        IsActive = address.IsActive
                    })
                    .ToList(),
                Contacts = item.Customer.Contacts
                    .OrderByDescending(contact => contact.IsActive)
                    .ThenByDescending(contact => contact.IsPrimary == true)
                    .ThenBy(contact => contact.ContactId)
                    .Select(contact => new CustomerContactDto
                    {
                        ContactId = contact.ContactId,
                        FirstName = contact.FirstName,
                        LastName = contact.LastName,
                        Gender = contact.Gender,
                        Phone = contact.Phone,
                        Email = contact.Email,
                        IsPrimary = contact.IsPrimary,
                        IsActive = contact.IsActive
                    })
                    .ToList(),
                Notes = item.Customer.CustomerNotes
                    .Where(note =>
                        note.CompanyId == scope.CompanyId &&
                        (scope.HasFullCustomerView ||
                         note.AuthorEmployeeId == scope.EmployeeId ||
                         viewerGroupIds.Contains(note.AuthorGroupId) ||
                         note.IsApprovedShare))
                    .OrderByDescending(note => note.CreatedAt)
                    .Select(note => new CustomerNoteDto
                    {
                        NoteId = note.Id,
                        Content = note.Content,
                        AuthorEmployeeId = note.AuthorEmployeeId,
                        AuthorEmployeeName = note.AuthorEmployee.FullName,
                        AuthorGroupId = note.AuthorGroupId,
                        CreatedAt = note.CreatedAt
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return detail is null
            ? OperationResult<CustomerDetailDto>.Fail("Customer was not found or is outside your visibility scope.")
            : OperationResult<CustomerDetailDto>.Ok(detail);
    }
}
