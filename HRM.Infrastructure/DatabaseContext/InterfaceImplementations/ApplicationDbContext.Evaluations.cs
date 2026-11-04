using HRM.Application.Abstractions.Persistence.DevAndQA;
using HRM.Domain.Entities.DevandqaSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext : IEvaluationDbContext
{
    DbSet<EvaluationStandard> IEvaluationDbContext.EvaluationStandards => Set<EvaluationStandard>();
}
