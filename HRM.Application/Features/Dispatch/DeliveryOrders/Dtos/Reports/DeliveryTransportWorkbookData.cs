using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports
{
    public class DeliveryTransportWorkbookData
    {
        public string CompanyName { get; set; } = string.Empty;
        public List<DeliveryTransportSummaryRow> SummaryRows { get; set; } = new();
        public Dictionary<string, List<DeliveryTransportReportRow>> DetailByDeliverer { get; set; } = new();
    }
}
