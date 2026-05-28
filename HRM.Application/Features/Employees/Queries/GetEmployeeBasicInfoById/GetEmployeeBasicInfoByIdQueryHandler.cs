using HRM.Application.Abstractions.Persistence.Employees;
using HRM.Application.Features.Employees.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Employees.Queries.GetEmployeeBasicInfoById
{
    internal sealed class GetEmployeeBasicInfoByIdQueryHandler
        : IRequestHandler<GetEmployeeBasicInfoByIdQuery, EmployeeBasicInfoDto?>
    {
        private readonly IEmployeeReadDbContext _dbContext;
        public GetEmployeeBasicInfoByIdQueryHandler(IEmployeeReadDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task<EmployeeBasicInfoDto?> Handle(
            GetEmployeeBasicInfoByIdQuery request,
            CancellationToken cancellationToken)
        {
            return await _dbContext.Employees
                .Where(x => x.EmployeeId == request.EmployeeId)
                .Select(x => new EmployeeBasicInfoDto
                {
                    Id = x.EmployeeId,
                    ExternalId = x.ExternalId,
                    FullName = x.FullName
                })
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
