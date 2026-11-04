# Authorization Organization

ACCUser có các quyền xem nghiệp vụ của President: pricing, IQC, thiết bị, production order, Formula/NVL/BOM, thông tin kỹ thuật và chi tiết rà soát giá NVL. ProductTechnicalViewers tách khỏi ProductTechnicalEditors. CRM, Sample Request, quotation, lookup, timeline và dashboard dùng BuildReadScopeAsync để xem toàn công ty; command tiếp tục dùng BuildScopeAsync và các gate ghi hiện có. Executive Pricing Review cho ACCUser đọc/preview qua GetReadContext, không mở gate ghi GetContext. Kho và phân bổ sale trong P&L dùng cùng quyền xem điều hành. Conversation Sample Request chỉ mở rộng đọc ở khu vực mà bộ lọc area cho phép; không mở khu vực riêng hay quyền quản lý participant. Seed/rollback hiện có được cập nhật cho claim mới, không tự chạy SQL. Với role đã migrate, triển khai code và chạy lại baseline seed idempotent trên AspNetRoleClaims trước khi login/refresh; token cũ không tự nhận quyền mới.

Private conversation areas use explicit active participants, not department roles or new permission claims. See [area contract](../../Features/InternalMail/AREAS.md). Company, parent activity and RD/color scopes still apply. Inviting a person grants that area's chat history, not access to other areas or pricing approval in business modules.

This folder is the backend source of truth for role names, role groups, policy names, and field visibility rules.

PLPUUser có ngoại lệ role grant cho `purchasing.purchase-order.manage`, áp dụng trước explicit permission set sau khi kiểm tra authenticated. Những capability khác vẫn theo claim/fallback hiện hành. Baseline và `20261017_SeedPlpuPurchasing.sql` đồng bộ claim DB; không tự chạy SQL. Gỡ claim riêng không thu hồi grant này: cần bỏ role hoặc thay đổi rule code. Company/employee/trạng thái vẫn enforce riêng.

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
price capability. Trong module giá, `ACCUser` có cùng các capability pricing với `President`, bao gồm workbench,
cost, margin, history, quản lý và duyệt giá. Product Pricing Workbench list/detail, Product Pricing Options, Quotation Pricing Workspace and
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

- Manufacturing workflow:
  - `plm.manufacturing-formula.manage`: `Developer`, `LabUser`.
  - `plm.manufacturing-adjustment.manage` / `plm.manufacturing-color-chip.manage`:
    `Admin`, `Developer`, `President`, `PLPUUser`, `QLSXUser`, `QCUser`, `LabUser`.
  - Chỉnh màu còn cần `plm.formula.material.view`; quyền quản lý không tự mở vật tư.
    Color-chip không chứa recipe/cost. History giá trả null nếu thiếu quyền giá.
  - Fallback, catalog và baseline seed/rollback được đồng bộ; chưa chạy SQL vào DB.

- Production order creators (`plm.production-order.create`):
  - `Admin`, `Developer`, `President`, `PLPUUser`, `QLSXUser`, `ManufactureUser`.
  - Cho phép thiết lập snapshot kỹ thuật, giá thỏa thuận và đơn giá công thức khi tạo MFG;
    không cấp quyền xem giá/cost trên GET khác. Handler kiểm tra company/active của reference.
  - Baseline seed/rollback hiện có được bổ sung claim; không tự chạy SQL.
    Với role đã dùng permission DB, cấp claim và login/refresh trước khi sử dụng endpoint mới.

- Formula materials/NVL viewers:
  - `Admin`, `Developer`, `President`, `ACCUser`, `LabUser`, `SeePriceUser`
- Formula price viewers:
  - `Admin`, `Developer`, `President`, `PriceView`, `ACCUser`, `SeePriceUser`
- Material cost, material price review and material replacement viewers:
  - `Admin`, `Developer`, `President`, `ACCUser`, `SeePriceUser`
  - `LabUser` and `LabAdmin` do not receive material price/cost visibility from these role sets.
- Product technical editors:
  - `Admin`, `Developer`, `President`, `LabUser`, `PLPUUser`
  - Also allowed to view restricted product technical fields in Sample Request audit history.
- Formula selectors:
  - `Admin`, `Developer`, `President`, `SaleUser`, `Leader`
- Formula material viewers gồm `Admin`, `Developer`, `President`, `LabUser` và
  `ACCUser`, `SeePriceUser`; nhóm này cũng được dùng cho quyền xem sample production order.
- Sample production order managers reuse `ApplicationRoleSets.PLM.ProductTechnicalEditors`.

## Employee Administration Defaults

- Employee viewers (detail, account permissions and role lookup): `Admin`, `Developer`, `President`, `ACCUser`.
- Employee managers: `Admin`, `Developer`, `President`.
- Global company manager: `Developer`.
- Role type and privileged-role managers: `Admin`, `Developer`.
- Privileged roles: `Admin`, `Developer`, `President`.
- `President` manages employees in the current company but cannot see, grant, or revoke privileged roles.

## Sales report profit

`reports.sales-profit.view` cho phép Sales Header trả giá vốn, lợi nhuận và biên lợi nhuận trong company/customer scope hiện hành. Mapping mặc định: President, ACCUser, Developer; SaleUser/Admin không tự được cấp. Token có explicit permission set dùng claim; thiếu claim trả null và không query cost. Baseline seed/rollback hiện có đã thêm claim, không tự chạy SQL. Môi trường đã migrate cần chạy lại seed và login/refresh token. Capability không đổi contract Executive P&L legacy.

## Quyền COA và danh mục đánh giá dùng chung

COA dùng dev-and-qa.coa.view/manage riêng với IQC. Viewer: Admin, Developer, President, ACCUser, QCUser, LabUser; manager: Admin, Developer, President, QCUser. Danh mục chỉ tiêu/phương pháp đo chấp nhận quyền tương ứng của IQC hoặc COA; quyền danh mục không mở quyền phiếu/template khác loại. Baseline SQL claim đã bổ sung nhưng chưa chạy. Account dùng permission từ DB cần cập nhật claims và refresh token. Xem [contract API](../../Features/DevAndQA/Shared/Evaluations/API.md).

LabUser được cấp dev-and-qa.iqc.view/manage và dev-and-qa.coa.view; không cấp coa.manage. Quyền xem bao gồm detail/history/export/attachments theo company scope hiện hành. Đã cập nhật baseline seed SQL, chưa chạy DB; token có explicit permission set cần được bổ sung role claims và login/refresh token. LabAdmin không tự được cấp trong thay đổi này.

LabUser được tạo/sửa QC, lưu đánh giá và upload/xóa tài liệu IQC trong company. Danh mục tiêu chuẩn cho quản lý qua IQC hoặc COA, mã sau tạo bất biến; POST vẫn yêu cầu code từ FE. Template: LabUser chỉ sửa nội dung Draft đúng loại, không tạo hoặc đổi mã; Published/Retired chỉ Developer hoặc QCUser có quyền quản lý đúng loại được PATCH. Role khác giữ mapping hiện hành. Seed mới cần áp dụng vào DB và login/refresh token.

Quyền template: chỉ Developer có capability quản lý đúng loại được tạo hoặc đổi Code. QCUser có capability quản lý đúng loại sửa Name/Items ở Draft/Published/Retired. Type/Version/Status không có trong PATCH; Retired giữ trạng thái, snapshot phiếu đã lưu không đổi. Items null/bỏ qua giữ danh sách, gửi mảng thay toàn bộ, [] bỏ hết; ID dòng giữ theo EvaluationStandardId, không trùng/tối đa 200, thứ tự 1..N theo mảng. Configuration override/clearFields giữ contract hiện có. Publish/retire giữ gate cũ.

Rollout thay đổi này: triển khai lại backend. Không thêm/đổi permission claim; QCUser đã có manage đúng loại và role trong token thì không cần đăng nhập lại. Nếu token thiếu role/capability cần cập nhật claims hiện có rồi login/refresh; không cấp chéo loại hoặc company.


### LabUser tương đương QCUser trong DevAndQA

LabUser có quyền DevAndQA tương đương QCUser: View/Manage IQC và COA; sửa/đồng bộ nội dung mẫu Draft/Published/Retired; publish/retire theo capability như QCUser. Chỉ Developer vẫn được tạo/đổi mã template. Ngoại lệ role-only đồng bộ áp dụng cả LabUser không có manage claim; API khác vẫn kiểm tra claims hiện hành. Seed đã bổ sung LabUser dev-and-qa.coa.manage nhưng chưa chạy DB; token permission-model=1 cần cập nhật claim và login/refresh cho API COA thông thường. Giữ company/employee scope.

## Quyền đọc báo cáo PLPU

`reports.plpu.view`: chỉ Developer, President, PLPUUser; bắt buộc role trong tập này kể cả token có stale claim. Runtime dùng permission claim, fallback dùng `ApplicationRoleSets.Reports.PlpuReaders`. Các API reports/plpu giữ company scope và không dùng quyền quản lý PO thay thế quyền đọc report. Quyền đọc bao gồm giá/cost trong report. PLPUUser đồng thời có role grant mua hàng riêng như mô tả ở trên. Seed baseline và script rollout `20261015_SeedPlpuReportView.sql` được cung cấp; chưa chạy DB. Sau seed cần login/refresh token để nhận claim report mới.


Quotation send confirmation: capability `crm.quotation.send.confirm` maps only to President through
ApplicationRoleSets.Quotations.SendConfirmers and ApplicationPermissionRoleSets. Baseline role-claim seed/rollback
include the permission without schema changes; SQL has not been executed automatically. Explicit-permission tokens
must contain this claim after login/refresh. Sale/Developer/anonymous are denied. Company/customer visibility and
pending request/message/concurrency checks remain required independently of the permission.
