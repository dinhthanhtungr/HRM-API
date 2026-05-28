using System;
using System.Collections.Generic;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.HrSchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.OrderSchema;

namespace HRM.Domain.Entities.SampleRequestSchema;

public partial class Formula
{
    public Guid FormulaId { get; set; }

    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Guid ProductId { get; set; }
    public string Status { get; set; } = "Draft";

    public Guid? CheckBy { get; set; }          // UNIQUEIDENTIFIER
    public DateTime? CheckDate { get; set; }       // DATETIME

    public Guid? SentBy { get; set; }          // UNIQUEIDENTIFIER
    public DateTime? SentDate { get; set; }       // DATETIME

    public decimal TotalPrice { get; set; }

    public DateTime? EffectiveDate { get; set; }
    public decimal? ProductionPrice { get; set; }
    public decimal? PresidentPrice { get; set; }
    public decimal? ProfitMarginPrice { get; set; }

    public bool IsSelect { get; set; } = false;
    public bool IsActive { get; set; } = true;
    //public bool? IsCustomerSelect { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedDate { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public Guid? CompanyId { get; set; }


    public virtual Company? Company { get; set; }

    public virtual Employee? CreatedByNavigation { get; set; }

    public virtual ICollection<FormulaMaterial> FormulaMaterials { get; set; } = new List<FormulaMaterial>();
    public virtual ICollection<ManufacturingFormula> ManufacturingFormulaSources { get; set; } = new List<ManufacturingFormula>();
    public virtual ICollection<MfgProductionOrder> ManufacturingProductionOrders { get; set; } = new List<MfgProductionOrder>();

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<SampleRequest> SampleRequests { get; set; } = new List<SampleRequest>();
    //public virtual ICollection<FormulaStatusLog> StatusLogs { get; set; } = new List<FormulaStatusLog>();
    public virtual ICollection<MerchandiseOrderDetail> MerchandiseOrderDetails { get; set; } = new List<MerchandiseOrderDetail>();
    public virtual ICollection<ManufacturingVUFormula> ManufacturingVUFormulas { get; set; } = new List<ManufacturingVUFormula>();
    public virtual Employee? UpdatedByNavigation { get; set; }
    public virtual Employee? CheckByNavigation { get; set; }
    public virtual Employee? SentByNavigation { get; set; }

}
