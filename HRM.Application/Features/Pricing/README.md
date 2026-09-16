# Pricing Authorization Boundary

Pricing is a cross-module business capability used by CRM Quotations, PLM Sample Requests/Formulas and Executive
pricing review. Its shared authorization decision lives in `Authorization/`, while pricing calculation and query
services remain in their owning feature folders.

## Boundary

- `Authorization/` contains `PricingAccessDecision`, `IPricingVisibilityService`, `PricingVisibilityService` and
  `PricingAccessScopes`.
- `Commons/Authorization` contains only the generic capability resolver, claim contract, role compatibility mapping
  and permission catalog. It must not grow feature-specific pricing field rules.
- CRM/PLM/Executive handlers must use the shared pricing decision and still perform their own company, customer,
  ownership and record-state checks.

## Capability contract

The pricing capabilities are declared in `Commons/Authorization/ApplicationPermissions.cs` and are stored at runtime
as Identity `AspNetRoleClaims` with `ClaimType = permission`. Approved selling price, system-calculated price,
material cost, manufacturing cost, margin, history, management and approval are independent capabilities.

For example, Sale can view an approved selling price but cannot view system-calculated price or internal cost. A
feature must mask its DTO at the backend and should skip realtime/cost queries when the corresponding capability is
false. See the CRM Quotations and PLM Sample Requests READMEs for endpoint-specific response semantics.

## Adding a pricing consumer

1. Identify the exact fields and capability needed.
2. Inject `IPricingVisibilityService` or use a narrower feature rule.
3. Apply company/customer/ownership scope separately.
4. Project only allowed fields; do not return EF entities.
5. Add a role matrix test and update the feature README when the public response changes.

