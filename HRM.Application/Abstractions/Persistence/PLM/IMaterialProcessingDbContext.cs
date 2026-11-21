using HRM.Domain.Entities.MaterialSchema;
using Microsoft.EntityFrameworkCore;

namespace HRM.Application.Abstractions.Persistence.PLM;

public interface IMaterialProcessingDbContext : IPLMWriteDbContext
{
    DbSet<MaterialProcessingFormula> MaterialProcessingFormulas { get; }
    DbSet<MaterialProcessingFormulaDetail> MaterialProcessingFormulaDetails { get; }
}
