using HRM.Application.Features.DevAndQA.ProductInspections.Dtos;
using HRM.Domain.Entities.DevandqaSchema;

namespace HRM.Application.Features.DevAndQA.ProductInspections;

internal static class ProductInspectionMutation
{
    internal static readonly IReadOnlySet<string> ClearableFields = new HashSet<string>(
        [
            "weight", "manufacturingDate", "expiryDate", "shape", "isShapePass",
            "particleSize", "isParticleSizePass", "packingSpec", "isPackingSpecPass",
            "visualCheck", "colorDeltaE", "isColorDeltaEPass", "moisture", "isMoisturePass",
            "mfr", "isMfrPass", "flexuralStrength", "isFlexuralStrengthPass", "elongation",
            "isElongationPass", "hardness", "isHardnessPass", "density", "isDensityPass",
            "tensileStrength", "isTensileStrengthPass", "flexuralModulus",
            "isFlexuralModulusPass", "impactResistance", "isImpactResistancePass",
            "antistatic", "isAntistaticPass", "storageCondition", "isStorageConditionPass",
            "intrinsicViscosity", "isIntrinsicViscosity", "meshType", "isMeshAttached",
            "dwellTime", "blackDots", "migrationTest", "defectImpurity", "defectBlackDot",
            "defectShortFiber", "defectMoist", "defectDusty", "defectWrongColor", "types",
            "deliveryAccepted", "notes"
        ],
        StringComparer.OrdinalIgnoreCase);

    internal static string? Validate(ProductInspectionWriteRequest request, bool creating)
    {
        if (creating && (!request.ProductStandardId.HasValue || request.ProductStandardId.Value == Guid.Empty))
        {
            return "ProductStandardId is required.";
        }

        if (creating && string.IsNullOrWhiteSpace(request.BatchId))
        {
            return "BatchId is required.";
        }

        if (request.ProductStandardId == Guid.Empty)
        {
            return "ProductStandardId is invalid.";
        }

        if (request.Weight is <= 0)
        {
            return "Weight must be greater than zero.";
        }

        if (request.ManufacturingDate.HasValue && request.ExpiryDate.HasValue &&
            request.ExpiryDate.Value < request.ManufacturingDate.Value)
        {
            return "ExpiryDate cannot be earlier than ManufacturingDate.";
        }

        var clearFields = NormalizeClearFields(request.ClearFields);
        if (creating && clearFields.Count > 0)
        {
            return "clearFields is only supported by PATCH.";
        }

        var blankProperty = typeof(ProductInspectionWriteRequest)
            .GetProperties()
            .FirstOrDefault(property => property.PropertyType == typeof(string) &&
                property.GetValue(request) is string value && string.IsNullOrWhiteSpace(value));
        if (blankProperty is not null)
        {
            return $"{blankProperty.Name} cannot be blank. Use clearFields to remove it.";
        }
        var unsupported = clearFields.Where(field => !ClearableFields.Contains(field)).ToArray();
        if (unsupported.Length > 0)
        {
            return $"Unsupported clearFields: {string.Join(", ", unsupported)}.";
        }

        var conflicts = clearFields
            .Where(field => typeof(ProductInspectionWriteRequest)
                .GetProperty(field, System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.IgnoreCase)
                ?.GetValue(request) is not null)
            .ToArray();
        if (conflicts.Length > 0)
        {
            return $"Fields cannot contain values and appear in clearFields at the same time: {string.Join(", ", conflicts)}.";
        }

        return null;
    }

    internal static IReadOnlySet<string> NormalizeClearFields(IReadOnlyList<string>? fields)
        => new HashSet<string>(fields ?? [], StringComparer.OrdinalIgnoreCase);

    internal static bool Apply(ProductInspection entity, ProductInspectionWriteRequest request)
    {
        var changed = false;
        changed |= PatchHelper.SetTrimmed(request.BatchId, () => entity.BatchId, value => entity.BatchId = value);
        changed |= PatchHelper.SetIfHasValue(request.Weight, () => entity.Weight ?? 0, value => entity.Weight = value);
        changed |= PatchHelper.SetIfHasValue(request.ManufacturingDate, () => entity.ManufacturingDate ?? default, value => entity.ManufacturingDate = value);
        changed |= PatchHelper.SetIfHasValue(request.ExpiryDate, () => entity.ExpiryDate ?? default, value => entity.ExpiryDate = value);
        changed |= PatchHelper.SetTrimmed(request.Shape, () => entity.Shape, value => entity.Shape = value);
        changed |= PatchHelper.SetIfHasValue(request.IsShapePass, () => entity.IsShapePass ?? false, value => entity.IsShapePass = value);
        changed |= PatchHelper.SetTrimmed(request.ParticleSize, () => entity.ParticleSize, value => entity.ParticleSize = value);
        changed |= PatchHelper.SetIfHasValue(request.IsParticleSizePass, () => entity.IsParticleSizePass ?? false, value => entity.IsParticleSizePass = value);
        changed |= PatchHelper.SetTrimmed(request.PackingSpec, () => entity.PackingSpec, value => entity.PackingSpec = value);
        changed |= PatchHelper.SetIfHasValue(request.IsPackingSpecPass, () => entity.IsPackingSpecPass ?? false, value => entity.IsPackingSpecPass = value);
        changed |= PatchHelper.SetIfHasValue(request.VisualCheck, () => entity.VisualCheck ?? false, value => entity.VisualCheck = value);
        changed |= PatchHelper.SetTrimmed(request.ColorDeltaE, () => entity.ColorDeltaE, value => entity.ColorDeltaE = value);
        changed |= PatchHelper.SetIfHasValue(request.IsColorDeltaEPass, () => entity.IsColorDeltaEpass ?? false, value => entity.IsColorDeltaEpass = value);
        changed |= PatchHelper.SetTrimmed(request.Moisture, () => entity.Moisture, value => entity.Moisture = value);
        changed |= PatchHelper.SetIfHasValue(request.IsMoisturePass, () => entity.IsMoisturePass ?? false, value => entity.IsMoisturePass = value);
        changed |= PatchHelper.SetTrimmed(request.Mfr, () => entity.Mfr, value => entity.Mfr = value);
        changed |= PatchHelper.SetIfHasValue(request.IsMfrPass, () => entity.IsMfrpass ?? false, value => entity.IsMfrpass = value);
        changed |= PatchHelper.SetTrimmed(request.FlexuralStrength, () => entity.FlexuralStrength, value => entity.FlexuralStrength = value);
        changed |= PatchHelper.SetIfHasValue(request.IsFlexuralStrengthPass, () => entity.IsFlexuralStrengthPass ?? false, value => entity.IsFlexuralStrengthPass = value);
        changed |= PatchHelper.SetTrimmed(request.Elongation, () => entity.Elongation, value => entity.Elongation = value);
        changed |= PatchHelper.SetIfHasValue(request.IsElongationPass, () => entity.IsElongationPass ?? false, value => entity.IsElongationPass = value);
        changed |= PatchHelper.SetTrimmed(request.Hardness, () => entity.Hardness, value => entity.Hardness = value);
        changed |= PatchHelper.SetIfHasValue(request.IsHardnessPass, () => entity.IsHardnessPass ?? false, value => entity.IsHardnessPass = value);
        changed |= PatchHelper.SetTrimmed(request.Density, () => entity.Density, value => entity.Density = value);
        changed |= PatchHelper.SetIfHasValue(request.IsDensityPass, () => entity.IsDensityPass ?? false, value => entity.IsDensityPass = value);
        changed |= PatchHelper.SetTrimmed(request.TensileStrength, () => entity.TensileStrength, value => entity.TensileStrength = value);
        changed |= PatchHelper.SetIfHasValue(request.IsTensileStrengthPass, () => entity.IsTensileStrengthPass ?? false, value => entity.IsTensileStrengthPass = value);
        changed |= PatchHelper.SetTrimmed(request.FlexuralModulus, () => entity.FlexuralModulus, value => entity.FlexuralModulus = value);
        changed |= PatchHelper.SetIfHasValue(request.IsFlexuralModulusPass, () => entity.IsFlexuralModulusPass ?? false, value => entity.IsFlexuralModulusPass = value);
        changed |= PatchHelper.SetTrimmed(request.ImpactResistance, () => entity.ImpactResistance, value => entity.ImpactResistance = value);
        changed |= PatchHelper.SetIfHasValue(request.IsImpactResistancePass, () => entity.IsImpactResistancePass ?? false, value => entity.IsImpactResistancePass = value);
        changed |= PatchHelper.SetTrimmed(request.Antistatic, () => entity.Antistatic, value => entity.Antistatic = value);
        changed |= PatchHelper.SetIfHasValue(request.IsAntistaticPass, () => entity.IsAntistaticPass ?? false, value => entity.IsAntistaticPass = value);
        changed |= PatchHelper.SetTrimmed(request.StorageCondition, () => entity.StorageCondition, value => entity.StorageCondition = value);
        changed |= PatchHelper.SetIfHasValue(request.IsStorageConditionPass, () => entity.IsStorageConditionPass ?? false, value => entity.IsStorageConditionPass = value);
        changed |= PatchHelper.SetTrimmed(request.IntrinsicViscosity, () => entity.IntrinsicViscosity, value => entity.IntrinsicViscosity = value);
        changed |= PatchHelper.SetIfHasValue(request.IsIntrinsicViscosity, () => entity.IsIntrinsicViscosity ?? false, value => entity.IsIntrinsicViscosity = value);
        changed |= PatchHelper.SetTrimmed(request.MeshType, () => entity.MeshType, value => entity.MeshType = value);
        changed |= PatchHelper.SetIfHasValue(request.IsMeshAttached, () => entity.IsMeshAttached ?? false, value => entity.IsMeshAttached = value);
        changed |= PatchHelper.SetIfHasValue(request.DwellTime, () => entity.DwellTime ?? false, value => entity.DwellTime = value);
        changed |= PatchHelper.SetTrimmed(request.BlackDots, () => entity.BlackDots, value => entity.BlackDots = value);
        changed |= PatchHelper.SetIfHasValue(request.MigrationTest, () => entity.MigrationTest ?? false, value => entity.MigrationTest = value);
        changed |= PatchHelper.SetIfHasValue(request.DefectImpurity, () => entity.DefectImpurity ?? false, value => entity.DefectImpurity = value);
        changed |= PatchHelper.SetIfHasValue(request.DefectBlackDot, () => entity.DefectBlackDot ?? false, value => entity.DefectBlackDot = value);
        changed |= PatchHelper.SetIfHasValue(request.DefectShortFiber, () => entity.DefectShortFiber ?? false, value => entity.DefectShortFiber = value);
        changed |= PatchHelper.SetIfHasValue(request.DefectMoist, () => entity.DefectMoist ?? false, value => entity.DefectMoist = value);
        changed |= PatchHelper.SetIfHasValue(request.DefectDusty, () => entity.DefectDusty ?? false, value => entity.DefectDusty = value);
        changed |= PatchHelper.SetIfHasValue(request.DefectWrongColor, () => entity.DefectWrongColor ?? false, value => entity.DefectWrongColor = value);
        changed |= PatchHelper.SetTrimmed(request.Types, () => entity.Types, value => entity.Types = value);
        changed |= PatchHelper.SetIfHasValue(request.DeliveryAccepted, () => entity.DeliveryAccepted ?? false, value => entity.DeliveryAccepted = value);
        changed |= PatchHelper.SetTrimmed(request.Notes, () => entity.Notes, value => entity.Notes = value);

        return ApplyClears(entity, NormalizeClearFields(request.ClearFields)) || changed;
    }

    private static bool ApplyClears(ProductInspection entity, IReadOnlySet<string> fields)
    {
        var changed = false;
        foreach (var field in fields)
        {
            changed |= field.ToLowerInvariant() switch
            {
                "weight" => Clear(() => entity.Weight, value => entity.Weight = value),
                "manufacturingdate" => Clear(() => entity.ManufacturingDate, value => entity.ManufacturingDate = value),
                "expirydate" => Clear(() => entity.ExpiryDate, value => entity.ExpiryDate = value),
                "shape" => ClearRef(() => entity.Shape, value => entity.Shape = value),
                "isshapepass" => Clear(() => entity.IsShapePass, value => entity.IsShapePass = value),
                "particlesize" => ClearRef(() => entity.ParticleSize, value => entity.ParticleSize = value),
                "isparticlesizepass" => Clear(() => entity.IsParticleSizePass, value => entity.IsParticleSizePass = value),
                "packingspec" => ClearRef(() => entity.PackingSpec, value => entity.PackingSpec = value),
                "ispackingspecpass" => Clear(() => entity.IsPackingSpecPass, value => entity.IsPackingSpecPass = value),
                "visualcheck" => Clear(() => entity.VisualCheck, value => entity.VisualCheck = value),
                "colordeltae" => ClearRef(() => entity.ColorDeltaE, value => entity.ColorDeltaE = value),
                "iscolordeltaepass" => Clear(() => entity.IsColorDeltaEpass, value => entity.IsColorDeltaEpass = value),
                "moisture" => ClearRef(() => entity.Moisture, value => entity.Moisture = value),
                "ismoisturepass" => Clear(() => entity.IsMoisturePass, value => entity.IsMoisturePass = value),
                "mfr" => ClearRef(() => entity.Mfr, value => entity.Mfr = value),
                "ismfrpass" => Clear(() => entity.IsMfrpass, value => entity.IsMfrpass = value),
                "flexuralstrength" => ClearRef(() => entity.FlexuralStrength, value => entity.FlexuralStrength = value),
                "isflexuralstrengthpass" => Clear(() => entity.IsFlexuralStrengthPass, value => entity.IsFlexuralStrengthPass = value),
                "elongation" => ClearRef(() => entity.Elongation, value => entity.Elongation = value),
                "iselongationpass" => Clear(() => entity.IsElongationPass, value => entity.IsElongationPass = value),
                "hardness" => ClearRef(() => entity.Hardness, value => entity.Hardness = value),
                "ishardnesspass" => Clear(() => entity.IsHardnessPass, value => entity.IsHardnessPass = value),
                "density" => ClearRef(() => entity.Density, value => entity.Density = value),
                "isdensitypass" => Clear(() => entity.IsDensityPass, value => entity.IsDensityPass = value),
                "tensilestrength" => ClearRef(() => entity.TensileStrength, value => entity.TensileStrength = value),
                "istensilestrengthpass" => Clear(() => entity.IsTensileStrengthPass, value => entity.IsTensileStrengthPass = value),
                "flexuralmodulus" => ClearRef(() => entity.FlexuralModulus, value => entity.FlexuralModulus = value),
                "isflexuralmoduluspass" => Clear(() => entity.IsFlexuralModulusPass, value => entity.IsFlexuralModulusPass = value),
                "impactresistance" => ClearRef(() => entity.ImpactResistance, value => entity.ImpactResistance = value),
                "isimpactresistancepass" => Clear(() => entity.IsImpactResistancePass, value => entity.IsImpactResistancePass = value),
                "antistatic" => ClearRef(() => entity.Antistatic, value => entity.Antistatic = value),
                "isantistaticpass" => Clear(() => entity.IsAntistaticPass, value => entity.IsAntistaticPass = value),
                "storagecondition" => ClearRef(() => entity.StorageCondition, value => entity.StorageCondition = value),
                "isstorageconditionpass" => Clear(() => entity.IsStorageConditionPass, value => entity.IsStorageConditionPass = value),
                "intrinsicviscosity" => ClearRef(() => entity.IntrinsicViscosity, value => entity.IntrinsicViscosity = value),
                "isintrinsicviscosity" => Clear(() => entity.IsIntrinsicViscosity, value => entity.IsIntrinsicViscosity = value),
                "meshtype" => ClearRef(() => entity.MeshType, value => entity.MeshType = value),
                "ismeshattached" => Clear(() => entity.IsMeshAttached, value => entity.IsMeshAttached = value),
                "dwelltime" => Clear(() => entity.DwellTime, value => entity.DwellTime = value),
                "blackdots" => ClearRef(() => entity.BlackDots, value => entity.BlackDots = value),
                "migrationtest" => Clear(() => entity.MigrationTest, value => entity.MigrationTest = value),
                "defectimpurity" => Clear(() => entity.DefectImpurity, value => entity.DefectImpurity = value),
                "defectblackdot" => Clear(() => entity.DefectBlackDot, value => entity.DefectBlackDot = value),
                "defectshortfiber" => Clear(() => entity.DefectShortFiber, value => entity.DefectShortFiber = value),
                "defectmoist" => Clear(() => entity.DefectMoist, value => entity.DefectMoist = value),
                "defectdusty" => Clear(() => entity.DefectDusty, value => entity.DefectDusty = value),
                "defectwrongcolor" => Clear(() => entity.DefectWrongColor, value => entity.DefectWrongColor = value),
                "types" => ClearRef(() => entity.Types, value => entity.Types = value),
                "deliveryaccepted" => Clear(() => entity.DeliveryAccepted, value => entity.DeliveryAccepted = value),
                "notes" => ClearRef(() => entity.Notes, value => entity.Notes = value),
                _ => false
            };
        }

        return changed;
    }

    private static bool Clear<T>(Func<T?> current, Action<T?> apply) where T : struct
    {
        if (!current().HasValue) return false;
        apply(null);
        return true;
    }

    private static bool ClearRef<T>(Func<T?> current, Action<T?> apply) where T : class
    {
        if (current() is null) return false;
        apply(null);
        return true;
    }

    private static class PatchHelper
    {
        internal static bool SetIfHasValue<T>(
            T? incoming,
            Func<T> current,
            Action<T> apply)
            where T : struct
        {
            if (!incoming.HasValue) return false;
            apply(incoming.Value);
            return true;
        }

        internal static bool SetTrimmed(
            string? incoming,
            Func<string?> current,
            Action<string?> apply)
            => HRM.Application.Commons.Patching.PatchHelper.SetTrimmed(incoming, current, apply);
    }
}
