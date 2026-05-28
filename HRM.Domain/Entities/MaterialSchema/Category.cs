using System;
using System.Collections.Generic;
using HRM.Domain.Entities.CompanySchema;
using HRM.Domain.Entities.ManufacturingSchema;
using HRM.Domain.Entities.SampleRequestSchema;

namespace HRM.Domain.Entities.MaterialSchema;

public partial class Category
{
    public Guid CategoryId { get; set; }

    public string? ExternalId { get; set; }

    public string? Types { get; set; }

    public string? Name { get; set; }

    public Guid CompanyId { get; set; }

    public bool? IsActive { get; set; }

    public virtual Company Company { get; set; } = null!;

    public virtual ICollection<Material> Materials { get; set; } = new List<Material>();
    public virtual ICollection<FormulaMaterial> FormulaMaterials { get; set; } = new List<FormulaMaterial>();
    public virtual ICollection<ManufacturingFormulaMaterial> ManufacturingFormulaMaterials { get; set; } = new List<ManufacturingFormulaMaterial>();
    public virtual ICollection<ManufacturingFormulaVersionItem> Items { get; set; } = new List<ManufacturingFormulaVersionItem>();
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
