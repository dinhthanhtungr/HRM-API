using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.CustomerEnum
{
    public enum QuotationStatus
    {
        Draft = 0,
        PendingApproval = 10,
        Approved = 20,
        Sent = 30,
        Negotiating = 40,
        Accepted = 50,
        Rejected = 60,
        Expired = 70,
        Cancelled = 80
    }
}
