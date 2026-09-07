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
- Business permission checks must use a named role set that describes the action, not the raw role names.
  - Good: `_currentUser.IsInAnyRole(ApplicationRoleSets.Notifications.RecipientManagers)`.
  - Avoid: `_currentUser.IsInRole(ApplicationRoles.President) || _currentUser.IsInRole(ApplicationRoles.Developer)` inside feature code.
- Put broad/global override roles in `ApplicationRoleSets.<Feature>.<ActionName>`.
  - Example: `ApplicationRoleSets.Notifications.RecipientManagers`.
- If a permission depends on company, group, team, department, ownership, active employee state, or database relationships, keep that logic in a DI service or feature authorization service. Do not hide DB-dependent authorization in `ApplicationRoleSets`.
- FE role gates are for UX only. BE authorization is the security boundary.

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
  - `Admin`, `Developer`, `President`, `PLPUUser`, `ACCUser`
- Formula price viewers:
  - `Admin`, `Developer`, `President`, `PriceView`, `ACCUser`, `SeePriceUser`
- Product technical editors:
  - `Admin`, `Developer`, `President`, `LabUser`
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
