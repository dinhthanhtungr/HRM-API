using HRM.Application.Abstractions.Persistence.PLM;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs;

public partial class ApplicationDbContext :
    IPLMReadDbContext,
    IPLMWriteDbContext
{
}
