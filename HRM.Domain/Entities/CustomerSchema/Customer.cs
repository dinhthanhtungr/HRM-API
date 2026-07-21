using System;
using System.Collections.Generic;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.DevandqaSchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;
using HRM.Domain.Entities.SampleRequestSchema;
using HRM.Domain.Enums.CustomerEnum;

namespace HRM.Domain.Entities.CustomerSchema;

public partial class Customer
{
    public Guid CustomerId { get; set; }
    public bool IsLead { get; set; }
    public LeadStatus LeadStatus { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string? CustomerGroup { get; set; }

    public string? ApplicationName { get; set; }

    public string? RegistrationNumber { get; set; }
    public string? RegistrationAddress { get; set; }

    public string? TaxNumber { get; set; }

    public string? Phone { get; set; }

    public string? Website { get; set; }

    public DateTime CreatedDate { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public Guid CompanyId { get; set; }

    public DateTime? IssueDate { get; set; }

    public string? IssuedPlace { get; set; }

    public string? FaxNumber { get; set; }

    public bool? IsActive { get; set; }


    public DateTime? LastContactDate { get; set; } // Ngày liên hệ cuối cùng
    public DateTime? NextFollowUpDate { get; set; } // Ngày theo dõi tiếp theo
    public string? CurrentCrmStatus { get; set; } // Trạng thái CRM hiện tại
    public Guid? CurrentSaleId { get; set; } // Nhân viên kinh doanh hiện tại

    public virtual ICollection<Address> Addresses { get; set; } = new List<Address>();
    public virtual ICollection<CustomerNote> CustomerNotes { get; set; } = new List<CustomerNote>();
    public virtual ICollection<CustomerClaim> CustomerClaims { get; set; } = new List<CustomerClaim>();
    public virtual ICollection<Contact> Contacts { get; set; } = new List<Contact>();
    public virtual ICollection<CustomerAssignment> CustomerAssignments { get; set; } = new List<CustomerAssignment>();
    public virtual ICollection<CustomerInteraction> CustomerInteractions { get; set; } = new List<CustomerInteraction>();
    public virtual ICollection<CustomerInteractionAiSummary> CustomerInteractionAiSummaries { get; set; } = new List<CustomerInteractionAiSummary>();
    public virtual ICollection<CustomerFollowUpTask> CustomerFollowUpTasks { get; set; } = new List<CustomerFollowUpTask>();
    public virtual ICollection<CustomerWorkPlan> CustomerWorkPlans { get; set; } = new List<CustomerWorkPlan>();
    public virtual ICollection<ColorChipRecord> ColorChipRecords { get; set; } = new List<ColorChipRecord>();
    public virtual ICollection<DetailCustomerTransfer> DetailCustomerTransfers { get; set; } = new List<DetailCustomerTransfer>();
    public virtual ICollection<MerchandiseOrder> MerchandiseOrders { get; set; } = new List<MerchandiseOrder>();
    public virtual ICollection<MfgProductionOrder> MfgProductionOrders { get; set; } = new List<MfgProductionOrder>();
    public virtual ICollection<SampleRequest> SampleRequests { get; set; } = new List<SampleRequest>();
    public virtual Company? Company { get; set; }
    public virtual Employee? CreatedByNavigation { get; set; }
    public virtual Employee? UpdatedByNavigation { get; set; }
}
