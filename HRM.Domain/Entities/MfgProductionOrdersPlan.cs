using System;
using System.Collections.Generic;

 namespace HRM.Domain.Entities;

public partial class MfgProductionOrdersPlan
{
    public Guid Id { get; set; }

    public string? Requirement { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public DateTime? CreatedDate { get; set; }

    public string? ExternalId { get; set; }

    public string? ProductExternalId { get; set; }

    public string? ProductName { get; set; }

    public string? ProductExpiryType { get; set; }

    public string? ProductPackage { get; set; }

    public bool? ProductRohsStandard { get; set; }

    public double? ProductRecycleRate { get; set; }

    public double? ProductWeight { get; set; }

    public string? ProductCustomerExternalId { get; set; }

    public double? ProductMaxTemp { get; set; }

    public double? ProductAddRate { get; set; }

    public Guid? ProductId { get; set; }
}
