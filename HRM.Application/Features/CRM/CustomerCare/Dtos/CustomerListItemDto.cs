using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Features.CRM.CustomerCare.Dtos
{

    public sealed class CustomerListItemDto
    {
        public Guid CustomerId { get; init; }
        public string ExternalId { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;

        public bool IsLead { get; init; }
        public string LeadStatus { get; init; } = string.Empty;
        public string TaxNumber {  get; init; } = string.Empty;

        public string? CustomerGroup { get; init; }
        public string? ApplicationName { get; init; }
        public string? Phone { get; init; }

        public string? CurrentCrmStatus { get; init; }
        public Guid? CurrentSaleId { get; init; }
        public string? CurrentSaleName { get; init; }

        public DateTime? LastContactDate { get; init; }
        public DateTime? NextFollowUpDate { get; init; }
        public DateTime? endLeadTime { get; set; }

        public int OpenTaskCount { get; init; }

        public DateTime CreatedDate { get; init; }
        public bool? IsActive { get; init; }
    }

}
