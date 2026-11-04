using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext
{
    public DbSet<CoaSpecification> CoaSpecifications { get; set; } = default!;
    public DbSet<CoaSpecificationItem> CoaSpecificationItems { get; set; } = default!;
    public DbSet<CoaCertificate> CoaCertificates { get; set; } = default!;
    public DbSet<CoaCertificateResult> CoaCertificateResults { get; set; } = default!;
}
