using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Application.Features.Dispatch.DeliveryOrders.Dtos.Reports;

namespace HRM.Application.Abstractions.Documents
{
    public interface IDeliveryReportExcelRenderer
    {
        byte[] ExportDeliveryPlanExcel(List<DeliveryPlanRow> rows);

        byte[] ExportDeliveryFinish(List<DeliveryFinishRow> rows, DateTime fromDate, DateTime toDate);

        byte[] ExportTransportWorkbook(
            DeliveryTransportWorkbookData data,
            DateTime fromDate,
            DateTime toDate);
    }
}
