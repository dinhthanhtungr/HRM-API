using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.DevAndQA;

/// <summary>Phiếu test độc lập và dữ liệu đọc chung với IQC; cùng transaction evaluation hiện hành.</summary>
public interface ILabTestDbContext : IEvaluationDbContext
{
    DbSet<LabTest> LabTests { get; }
    DbSet<LabTestResult> LabTestResults { get; }
    DbSet<Material> Materials { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<ApplicationUser> Users { get; }
    DbSet<ApplicationRole> Roles { get; }
    DbSet<ApplicationUserRole> UserRoles { get; }
}
