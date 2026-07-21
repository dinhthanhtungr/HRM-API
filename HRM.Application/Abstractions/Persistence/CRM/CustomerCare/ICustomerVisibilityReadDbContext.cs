using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Entities.WorkTaskSchema;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Abstractions.Persistence.CRM.CustomerCare
{
    public interface ICustomerVisibilityReadDbContext
    {
        DbSet<Customer> Customers { get; }
        DbSet<CustomerAssignment> CustomerAssignments { get; }
        DbSet<CustomerClaim> CustomerClaims { get; }
        DbSet<MemberInGroup> MemberInGroups { get; }
        DbSet<SampleRequest> SampleRequests { get; }
        DbSet<MerchandiseOrder> MerchandiseOrders { get; }
        DbSet<CustomerInteraction> CustomerInteractions { get; }
        DbSet<WorkTask> WorkTasks { get; }
        DbSet<Employee> Employees { get; }
    }
}
