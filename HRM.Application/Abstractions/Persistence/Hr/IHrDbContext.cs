using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.Hr;

public interface IHrDbContext
{
    DbSet<Employee> Employees { get; }

    DbSet<Part> Parts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
