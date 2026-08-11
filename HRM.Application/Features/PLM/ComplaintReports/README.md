# PLM Complaint Reports

## Mục đích

Complaint Report ghi nhận phản ánh của khách hàng trên các SaleOrder detail đã giao, truy vết lot và lệnh sản xuất thực tế, sau đó đi qua điều tra, CAPA và phê duyệt trước khi quyết định có sản xuất bù hay không.

Sale chỉ ghi nhận nội dung tiếp nhận và đề nghị hướng xử lý. Sale không được gửi hoặc quyết định `Status`, `ResolutionType` cuối, approval, handling SaleOrder hay MFG.

## API tiếp nhận dành cho Sale

### Tạo và submit

`POST /api/v1/plm/complaint-reports` dùng `multipart/form-data`:

- `request`: JSON bắt buộc và không chấp nhận field ngoài contract.
- `files`: danh sách file tùy chọn, dùng attachment slot `Complaint`.

Contract `request`:

```json
{
  "customerId": "guid",
  "summary": "Tóm tắt phản ánh",
  "nonConformityDescription": "Mô tả sự không phù hợp",
  "requestedResolutionType": "ReplacementProduction",
  "requestedReplacementDeliveryDate": "2026-08-20T00:00:00",
  "lines": [
    {
      "sourceMerchandiseOrderDetailId": "guid",
      "complaintQuantity": 100,
      "issueType": "ColorMismatch",
      "severity": "High",
      "description": "Mô tả dòng",
      "lots": [
        {
          "sourceDeliveryOrderDetailId": "guid",
          "sourceLotConsumptionId": "guid hoặc null",
          "complaintQuantity": 100
        }
      ]
    }
  ]
}
```

Create luôn tạo report ở `Submitted`, còn `ResolutionType` cuối là `null`. Các field cũ `resolutionType`, `status`, `createHandlingOrder`, `requestedDeliveryDate`, audit và company không còn thuộc contract; JSON chứa field cũ sẽ bị từ chối.

BE tự lấy current `EmployeeId`/`CompanyId`, snapshot customer/creator/product/VU formula, MFG nguồn, ManufacturingFormula của `ProductionSelectVersion` hiện hành, snapshot lot và `AttachmentCollectionId`. POST không gọi `ComplaintHandlingOrderService` và không tạo MFG.

### Sửa bản Draft bị trả về

`PUT /api/v1/plm/complaint-reports/{id}/reception` nhận JSON gồm:

- `summary`
- `nonConformityDescription`
- `requestedResolutionType`
- `requestedReplacementDeliveryDate`
- `lines` và `lots` cùng contract với POST

Chỉ employee đã tạo report được sửa và chỉ khi `Status=Draft`. Customer, status, kết luận cuối, approval, attachment, handling order và MFG không được cập nhật bởi endpoint này.

Update dùng semantics thay thế toàn bộ: các line/lot active cũ được soft-deactivate và bộ line/lot mới được tạo trong cùng transaction. Nếu resolve hoặc lưu bất kỳ dòng nào thất bại, toàn bộ thay đổi rollback. Endpoint giữ report ở `Draft`; việc resubmit là workflow riêng.

Draft reception update và resubmit không tạo `CustomerInteraction`. Interaction loại `Complaint` được ghi khi create/submit, initial decision và final decision; các lần lưu nháp không tạo thêm interaction.

## Validation nguồn giao hàng

- Tất cả source line phải active, cùng customer/company và nằm trong customer/order visibility của user.
- Chỉ detail có delivery active, không phải attachment line và không bị `Canceled`/`Cancelled` mới được complaint.
- Delivery detail phải thuộc đúng source SaleOrder detail.
- Nếu `SourceLotConsumptionId` có giá trị, consumption phải active và thuộc đúng delivery detail.
- Nếu `SourceLotConsumptionId=null`, delivery detail không được có normalized lot consumption và phải có `LotNoList` để snapshot.
- Lookup source ưu tiên `DeliveryOrderDetailLotConsumption` active. Với delivery detail lịch sử chưa backfill, API trả một lot fallback có `lotConsumptionId=null`, `lotNo` lấy từ `LotNoList` và quantity bằng toàn bộ quantity của delivery detail.
- Mỗi lot quantity phải lớn hơn 0 và không vượt delivered quantity còn lại.
- Tổng lot quantity phải bằng `ComplaintQuantity` của line.
- Line quantity không vượt tổng delivered quantity còn lại.
- Lượng đã complaint của report active trước đó được trừ; report `Rejected` và `Cancelled` không chiếm lượng, report `Closed` vẫn chiếm lượng.
- Khi thay thế reception của chính report Draft, lượng cũ của report đó được loại khỏi phép tính để tránh tự trừ hai lần.
- Remaining quantity luôn được chặn tối thiểu bằng `0`.

## Quyền các API ghi liên quan

- `PUT /{id}/investigation` là endpoint duy nhất cập nhật nội dung điều tra.
- `POST /{id}/initial-decision` là endpoint duy nhất quyết định hướng xử lý cuối và tạo đơn sản xuất bù khi cần.
- `POST /{id}/final-decision` là endpoint duy nhất đóng hoặc trả lại complaint.
- Các route legacy `PUT /{id}/content`, `PATCH /{id}/complete` và `POST /{id}/handling-order` đã bị gỡ khỏi public API.
- Handling SaleOrder luôn có giá `0`, được auto approve và tạo MFG trong cùng transaction của initial replacement decision.

## Read API

- `GET /api/v1/plm/complaint-reports`: pagination và filter `customerId`, `status`, `resolutionType`, `reportedFrom`, `reportedTo`, `keyword`, `assignedToMe`, `sourceOrderId`.
- `GET /source-lines`: trả delivery details, lots, MFG/công thức thực tế, lượng đã complaint và remaining quantity.
- `GET /{id}`: trả reception, investigation, lines/lots, actions, risk/effectiveness, approval history, handling order/MFG, attachments và `allowedActions`.
- `GET /by-source-order/{merchandiseOrderId}`: trả badge/status cần cho timeline.

Mọi read/write API áp dụng company scope, customer visibility và không trả EF entity trực tiếp.

## Persistence

- `ComplaintReport`: header CAPA, đề nghị của Sale, kết luận cuối, risk/effectiveness và customer/creator snapshots.
- `ComplaintReportLine`: source SaleOrder detail, product/VU snapshots, source MFG và ManufacturingFormula thực tế.
- `ComplaintReportLineLot`: delivery detail/lot consumption, lot, delivered quantity/date và complaint quantity snapshot.
- `ComplaintCapaAction`: immediate hoặc corrective/preventive action.
- `ComplaintReportApproval`: lịch sử approval bất biến.

Status integer cố định: `Draft=0`, `Submitted=1`, `Investigating=2`, `Closed=3`, `Rejected=4`, `Cancelled=5`, `ActionInProgress=6`, `PendingVerification=7`, `PendingFinalApproval=8`.

Các property snapshot mới trên `ComplaintReport` cần được đưa vào migration/schema sync ở phase migration riêng. Phase này không tạo migration.

## PHASE 4 - Investigation and CAPA workflow

Authorization policies are independent from SaleOrder approval:

- `Complaint.Create`: `SaleUser`, `Admin`, `Developer`, `President`.
- `Complaint.Investigate`: `QCUser`, `Admin`, `Developer`, `President`.
- `Complaint.Action.Update`: authenticated at HTTP boundary; the handler requires the assignee or an investigation/management role.
- `Complaint.Verify`: `QCUser`, `Leader`, `Admin`, `Developer`, `President`.
- `Complaint.InitialApprove` and `Complaint.FinalApprove`: `Leader`, `Admin`, `Developer`, `President`.
- `Complaint.ViewPdf`: Sale, QC and approval groups. The PDF endpoint is implemented in the PDF phase.

Write endpoints:

- `PUT /{id}/investigation`: saves department snapshots, standards/scopes, investigation and risk review. Allowed only in `Investigating` or `ActionInProgress`.
- `PUT /{id}/capa-actions`: atomically replaces active immediate and corrective/preventive actions, resolves assignee names in BE, then moves to `ActionInProgress`.
- `PATCH /{id}/capa-actions/{actionId}/result`: only updates `result` and `completedAt`; it cannot change content or owner. `completedAt=null` records progress but does not complete the action.
- `POST /{id}/request-verification`: requires every active CAPA action to have both a result and completion time, then moves to `PendingVerification`.
- `PUT /{id}/effectiveness`: stores the BE-resolved person-in-charge snapshot and effectiveness result, then moves to `PendingFinalApproval`.
- `POST /{id}/final-decision`: final approval is accepted only from `PendingFinalApproval`.

Every PHASE 4 handler enforces company scope, permission and status independently of controller authorization. CAPA collection replacement is transactional, workflow mutations write `EventLog` with `EventType.ComplaintReport`, and investigation/CAPA draft saves do not create `CustomerInteraction`.

No migration is created in this phase.

## PHASE 5 - Decisions and replacement production

### Initial decision

`POST /api/v1/plm/complaint-reports/{id}/initial-decision` uses `Complaint.InitialApprove` and only accepts a `Submitted` report.

```json
{
  "decision": "Approved",
  "comment": "Approved for replacement production",
  "resolutionType": "ReplacementProduction",
  "lines": [
    {
      "complaintReportLineId": "guid",
      "approvedReplacementQuantity": 100
    }
  ]
}
```

- `Returned` writes approval history and moves the report to `Draft` for Sale correction.
- After correction, only the creator calls `POST /{id}/resubmit` to move `Draft` back to `Submitted`. This command writes EventLog but no CustomerInteraction.
- `Rejected` writes approval history and moves the report to `Rejected`.
- `Approved` writes `InitialHodApproval`, stores the approver-owned final `ResolutionType`, and moves to `Investigating`.
- A replacement decision must specify every active line exactly once with quantity `> 0` and `<= ComplaintQuantity`.
- Replacement approval creates or synchronizes one zero-value `OrderType.Complaint` SaleOrder, links every detail to one `ComplaintReportLineId`, automatically approves it, and creates exactly one MFG/link per active detail.
- The handling detail keeps the source VU `FormulaId`; MFG selection still uses the shared SaleOrder manufacturing flow.
- Decision, approval history, interactions, handling order, MFG, timeline, notification and outbox are committed in one transaction. Any failure rolls all of them back.

`SaleOrderApprovalService` now accepts a complaint order after valid initial replacement approval. It no longer requires the report to be `Closed`. Calling approval again for an already `Approved` complaint order is idempotent only when every active detail has exactly one active MFG link; missing or duplicate links fail.

### Final decision

`POST /api/v1/plm/complaint-reports/{id}/final-decision` uses `Complaint.FinalApprove` and only accepts `PendingFinalApproval`.

- `Approved` appends `FinalHodApproval`, moves to `Closed`, and sets `CompletedAt/CompletedBy`.
- `Returned` appends approval history and moves back to `ActionInProgress` without deleting prior approvals.
- Final decision writes CustomerInteraction, EventLog and notification in the same transaction.

`GET /{id}` exposes `allowedActions.canInitialDecision` and `allowedActions.canFinalDecision`; FE must use these flags instead of deriving role/status itself.

`PATCH /{id}/complete`, `PUT /{id}/content` and `POST /{id}/handling-order` are no longer public endpoints. FE must use `investigation`, `initial-decision` and `final-decision` exclusively.

All public complaint write commands enforce role, state transition, company scope and shared customer visibility inside the handler. Controller policy alone is never treated as the business authorization boundary.

## PHASE 6 - CAPA PDF

`GET /api/v1/plm/complaint-reports/{complaintReportId}/pdf` requires
`Complaint.ViewPdf` and returns `application/pdf` inline with file name
`CAPA-{ExternalId}.pdf`.

- The handler applies company scope and the same customer visibility rules as the
  complaint detail query. A report outside that scope is returned as not found.
- The handler builds a complete document DTO before rendering. The QuestPDF
  renderer does not access EF, storage, current user, or any other data source.
- Customer, product, VU formula, manufacturing formula, employee and department
  values are rendered from stored snapshots. Approval blocks show only actor
  snapshot, decision, date and comment; no signature is generated.
- Every status except `Closed` receives a bilingual `BẢN NHÁP / DRAFT` watermark.
- Attachments are always listed. Only valid PNG/JPEG evidence is embedded, with a
  maximum of 8 images, 3 MB per image and 12 MB total. Invalid or oversized image
  files remain listed and do not fail the PDF.
- The form contains the four VA-QMR-F31(05) sections: report information,
  evidence/initial approval, corrective/preventive actions, and effectiveness/final
  approval. Tables repeat headers and may continue onto overflow pages.
- Form titles, section labels, table headers, approval labels, empty states and the
  footer are rendered in Vietnamese/English. User-entered business content and
  stored snapshots are preserved verbatim and are never translated automatically.

No migration is required for this phase.

## Verification

```powershell
dotnet test HRM.Application.Tests\HRM.Application.Tests.csproj --no-restore -c Release
dotnet build HRM.sln --no-restore -c Release
```
