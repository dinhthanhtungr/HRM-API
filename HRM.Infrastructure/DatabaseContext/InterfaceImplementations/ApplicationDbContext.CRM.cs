using HRM.Application.Abstractions.Persistence.CRM.CustomerCare;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Infrastructure.DatabaseContext.ApplicationDbs
{
    public partial class ApplicationDbContext :
        ICRMReadDbContext,
        ICRMWriteDbContext
    {
    }
}
