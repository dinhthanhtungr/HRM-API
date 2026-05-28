using System;
using System.Collections.Generic;
using HRM.Domain.Entities.MaterialSchema;
using HRM.Domain.Enums.Formulas;

namespace HRM.Domain.Entities.SampleRequestSchema;

public partial class FormulaMaterial
{
    public Guid FormulaMaterialId { get; set; }

    public Guid FormulaId { get; set; }
    public Guid? MaterialId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? ProductId { get; set; }

    public decimal Quantity { get; set; }            // DECIMAL(18,6)
    public decimal UnitPrice { get; set; }           // DECIMAL(16,2)
    public decimal TotalPrice { get; set; }          // DECIMAL(16,2)

    public ItemType itemType { get; set; }     
    public string? MaterialNameSnapshot { get; set; }         // NVARCHAR
    public string? MaterialExternalIdSnapshot { get; set; }   // VARCHAR
    public string? Unit { get; set; }                         // VARCHAR
    public bool IsActive { get; set; } = true;
    public int LineNo { get; set; }


    public virtual Formula Formula { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;

    public virtual Material Material { get; set; } = null!;

    public virtual Category? Category { get; set; }
}
