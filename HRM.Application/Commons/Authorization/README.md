# Authorization Organization

This folder is the backend source of truth for role names, role groups, policy names, and field visibility rules.

## Concepts

- `ApplicationRoles`: exact role names stored in the database/JWT claims.
- `ApplicationRoleSets`: reusable role groups for modules and business actions.
- `PlmPolicies`: ASP.NET Core authorization policy names for PLM endpoints.
- `IPLMFieldVisibilityService`: field-level visibility for PLM DTOs, for example returning price fields as `null` when the user can view general data but cannot view prices.

## Rules

- Use endpoint policies when the whole API response is sensitive.
  - Example: formula material/NVL APIs use `PlmPolicies.ViewFormulaMaterials`.
- Use field visibility services when the API can return general data but must hide sensitive fields.
  - Example: sample request detail can return formula metadata while price fields are `null`.
- Do not hard-code role strings in controllers, handlers, or services.
  - Add the role to `ApplicationRoles`.
  - Add a role group to `ApplicationRoleSets`.
  - Use the role group from policy registration or visibility service.
- FE role gates are for UX only. BE authorization is the security boundary.

## PLM Current Defaults

- Formula materials/NVL viewers:
  - `Admin`, `Developer`, `President`, `PLPUUser`, `ACUser`
- Formula price viewers:
  - `Admin`, `Developer`, `President`, `PriceView`, `ACUser`, `SeePriceUser`
- Product technical editors:
  - `Admin`, `Developer`, `President`, `LabUser`
- Formula selectors:
  - `Admin`, `Developer`, `President`, `SaleUser`, `Leader`
