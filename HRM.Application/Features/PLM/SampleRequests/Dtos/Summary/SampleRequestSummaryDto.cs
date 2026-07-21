using HRM.Application.Features.PLM.SampleRequests.Dtos.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.PLM.SampleRequests.Dtos.Summary
{
    public class SampleRequestSummaryDto
    {
        public Guid SampleRequestId { get; set; }
        public string? ExternalId { get; set; }
        public Guid? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ColorValue { get; set; }
        public string? ColorDisplayName { get; set; }
        public string? AdditiveCode { get; set; }
        public string? AdditiveGroupCode { get; set; }
        public string? AdditiveDisplayName { get; set; }
        public string? ColourCode { get; set; }
        public string? Status { get; set; } = string.Empty;

        public string? CustomerName { get; set; } = string.Empty;
        public string? CustomerExternalId { get; set; } = string.Empty;

        public string? LabName { get; set; } = string.Empty;
        public string? CreatedBy { get; set; } = string.Empty;
        public string? EndUserName { get; set; } = string.Empty;
        

        public DateTime? ProductCreatedDate { get; set; }
        public DateTime? ProductUpdatedDate { get; set; }


        public DateTime? SampleRequestCreatedDate { get; set; }
        public DateTime? SampleRequestUpdatedDate { get; set; }


        public DateTime? ExpectedDeliveryDate { get; set; }
        public DateTime? RequestDeliveryDate { get; set; }
        public DateTime? RealDeliveryDate { get; set; }
        public DateTime? RealPriceQuoteDate { get; set; }
        public DateTime? ExpectedPriceQuoteDate { get; set; }

        public IReadOnlyList<SampleRequestAttachmentDto> Attachments { get; set; }
            = new List<SampleRequestAttachmentDto>();

        public SampleRequestSelectedFormulaDto? SelectedFormula { get; set; }

        public IReadOnlyList<SampleRequestProductionOrderDto> ProductionOrders { get; set; }
            = new List<SampleRequestProductionOrderDto>();
    }
}
