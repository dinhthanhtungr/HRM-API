using System;
using System.Collections.Generic;
using HRM.Domain.Entities.AttachmentSchema;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.CustomerSchema;
using HRM.Domain.Entities.HrSchema;

namespace HRM.Domain.Entities.SampleRequestSchema;

public partial class SampleRequest
{
    public Guid SampleRequestId { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public Guid ManagerBy { get; set; }

    public Guid ProductId { get; set; }
    public Guid AttachmentCollectionId { get; set; }

    public DateTime? RealDeliveryDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }

    public DateTime? RequestDeliveryDate { get; set; }
    public DateTime? RequestTestSampleDate { get; set; }
    public DateTime? ResponseDeliveryDate { get; set; }

    public DateTime? RealPriceQuoteDate { get; set; }
    public DateTime? ExpectedPriceQuoteDate { get; set; }

    public string RequestType { get; set; } = string.Empty;

    public double? ExpectedQuantity { get; set; }
    public decimal? ExpectedPrice { get; set; }
    public double? SampleQuantity { get; set; }
    public int? NumberDeliverySampleDate { get; set; }

    public string? OtherComment { get; set; }
    public string? InfoType { get; set; }

    public Guid? FormulaId { get; set; }

    public string? SaleComment { get; set; }

    public string? AdditionalComment { get; set; } = string.Empty;
    public string? CustomerProductCode { get; set; } = string.Empty;

    public Guid BranchId { get; set; }

    public string Status { get; set; } = "New";

    public string Package { get; set; } = string.Empty;
    public int BagWeight { get; set; }

    public DateTime CreatedDate { get; set; }

    public Guid CreatedBy { get; set; }

    public Guid? SendBy { get; set; }
    public DateTime? SendDate { get; set; }


    public DateTime? UpdatedDate { get; set; }

    public Guid? UpdatedBy { get; set; }

    public Guid CompanyId { get; set; }
    public bool IsActive { get; set; }

    public virtual AttachmentCollection AttachmentCollection { get; set; } = null!;
    //public virtual Branch Branch { get; set; } = null!;

    public virtual Company? Company { get; set; }
    public virtual Company? Branch { get; set; }

    public virtual Employee? CreatedByNavigation { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual Formula? Formula { get; set; }

    public virtual Employee ManagerByNavigation { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<QuotationLine> QuotationLines { get; set; } = [];
    public virtual ICollection<SampleRequestSampleTrial> SampleRequestSampleTrials { get; set; } = [];

    public virtual Employee? UpdatedByNavigation { get; set; }
    public virtual Employee? SendByNavigation { get; set; }
}
