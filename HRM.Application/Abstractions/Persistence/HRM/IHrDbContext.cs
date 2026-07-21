using HRM.Domain.Entities.HrSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.HRM;

public interface IHrDbContext
{
    DbSet<Employee> Employees { get; }

    DbSet<Part> Parts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
