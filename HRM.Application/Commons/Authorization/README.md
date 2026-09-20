# Authorization Organization

This folder is the backend source of truth for role names, role groups, policy names, and field visibility rules.

## Concepts

- `ApplicationRoles`: exact role names stored in the database/JWT claims.
- `ApplicationRoleSets`: reusable role groups for modules and business actions.
- `ApplicationPermissions`: stable capability names consumed by feature code.
- `ApplicationPermissionRoleSets`: compatibility mapping for tokens/roles not migrated to database permissions yet.
- `ApplicationPermissionClaimTypes`: JWT/Identity claim contract for database-backed permissions.
- `ICurrentUserPermissionService`: resolves current-user capabilities without exposing role checks to features.
- `Features/Pricing/Authorization/IPricingVisibilityService`: returns one field/action visibility decision for pricing DTOs and use cases.
- `PlmPolicies`: ASP.NET Core authorization policy names for PLM endpoints.
- `Features/PLM/Shared/Authorization/IPLMFieldVisibilityService`: field-level visibility for PLM DTOs, for example returning price fields as `null` when the user can view general data but cannot view prices.

## Rules

- Use endpoint policies when the whole API response is sensitive.
  - Example: formula material/NVL APIs use `PlmPolicies.ViewFormulaMaterials`.
- Use field visibility services when the API can return general data but must hide sensitive fields.
  - Example: sample request detail can return formula metadata while price fields are `null`.
- Do not hard-code role strings in controllers, handlers, or services.
  - Add the role to `ApplicationRoles`.
  - Add a role group to `ApplicationRoleSets`.
  - Use the role group from policy registration or visibility service.
- Business permission checks must use a named role set that describes the action, not the raw role names.
  - Good: `_currentUser.IsInAnyRole(ApplicationRoleSets.Notifications.RecipientManagers)`.
  - Avoid: `_currentUser.IsInRole(ApplicationRoles.President) || _currentUser.IsInRole(ApplicationRoles.Developer)` inside feature code.
- Put broad/global override roles in `ApplicationRoleSets.<Feature>.<ActionName>`.
  - Example: `ApplicationRoleSets.Notifications.RecipientManagers`.
- If a permission depends on company, group, team, department, ownership, active employee state, or database relationships, keep that logic in a DI service or feature authorization service. Do not hide DB-dependent authorization in `ApplicationRoleSets`.
- FE role gates are for UX only. BE authorization is the security boundary.

## Capability And Data Visibility

Role describes a user group; capability describes an allowed action or data class. New feature code should depend
on a capability or a feature visibility service rather than checking raw roles. Endpoint access, row/company scope,
field visibility and mutation authorization are separate checks; satisfying one does not imply the others.

Pricing capabilities distinguish approved selling price, system-calculated price, material/manufacturing cost,
margin, history, management and approval. `SaleUser` is intentionally allowed to view only the approved selling
price capability. Product Pricing Workbench list/detail, Product Pricing Options, Quotation Pricing Workspace and
Pricing Queue use `IPricingVisibilityService`. Other pricing endpoints must be migrated explicitly; defining a
capability alone does not protect a response.

PLM formula prices, formula materials and product technical information also have separate capabilities. The
existing `IPLMFieldVisibilityService` resolves those capabilities centrally, so Formula detail/list/version,
material preview, export and Sample Request consumers do not maintain independent role lists. Dispatch delivery
cost uses its own capability even though its initial role mapping intentionally matches formula-price viewers.

Runtime capability mapping is stored in ASP.NET Identity `AspNetRoleClaims` with `ClaimType = permission`. Login and
refresh resolve claims from active role assignments and place them in the access token. A role carrying
`permission-model = 1` uses the explicit database set, including an empty set; roles not migrated yet use
`ApplicationPermissionRoleSets` as a rollout compatibility fallback. Company, ownership, group membership and
record state remain DB-dependent checks and must not be encoded as static role claims.

The baseline seed and rollback are
`HRM.Infrastructure/DatabaseContext/Migrations/20260913_SeedRolePermissionClaims.sql` and its `.rollback.sql` pair.
Permission changes affect newly issued access tokens after login/refresh; they do not mutate an existing token.

## Notification Current Defaults

- Notification recipient managers:
  - `Developer`, `President`
  - May archive/remove a notification from another employee's inbox in the same company.
  - The notification creator can still archive/remove recipients for notifications they created.
- Internal Mail conversation participant managers:
  - `Developer`, `President`
  - May remove a non-owner participant from an active conversation in the same company.
  - The conversation owner can still remove non-owner participants from that conversation.

## PLM Current Defaults

- Formula materials/NVL viewers:
  - `Admin`, `Developer`, `President`, `SeePriceUser`
- Formula price viewers:
  - `Admin`, `Developer`, `President`, `PriceView`, `ACCUser`, `SeePriceUser`
- Material cost, material price review and material replacement viewers:
  - `Admin`, `Developer`, `President`, `SeePriceUser`
  - `LabUser` and `LabAdmin` do not receive material price/cost visibility from these role sets.
- Product technical editors:
  - `Admin`, `Developer`, `President`, `LabUser`, `PLPUUser`
  - Also allowed to view restricted product technical fields in Sample Request audit history.
- Formula selectors:
  - `Admin`, `Developer`, `President`, `SaleUser`, `Leader`
- Sample production order viewers reuse `ApplicationRoleSets.PLM.FormulaMaterialViewers`.
- Sample production order managers reuse `ApplicationRoleSets.PLM.ProductTechnicalEditors`.

## Employee Administration Defaults

- Employee managers: `Admin`, `Developer`, `President`.
- Global company manager: `Developer`.
- Role type and privileged-role managers: `Admin`, `Developer`.
- Privileged roles: `Admin`, `Developer`, `President`.
- `President` manages employees in the current company but cannot see, grant, or revoke privileged roles.
