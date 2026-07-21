# Sample Requests

## GetSampleRequestSummary Attachments

`GetSampleRequestSummary` returns sample request attachments as lightweight metadata under each summary item.

The attachment query links data through:

```text
SampleRequest.AttachmentCollectionId -> AttachmentModel
```

The response includes `attachments` with `url`, `downloadUrl`, and `isImage`. It does not include file bytes, base64, or physical storage paths.

Frontend image preview should use:

```html
<img src="/api/v1/attachments/{attachmentId}" alt="" />
```

This keeps the summary API fast and lets the browser load/cache images separately.

## Detail And Write APIs

Sample request detail is loaded by id:

```http
GET /api/v1/plm/sample-requests/{sampleRequestId}
```

Create a sample request:

```http
POST /api/v1/plm/sample-requests
```

Patch a sample request:

```http
PATCH /api/v1/plm/sample-requests/{sampleRequestId}
```

Read change history for one sample request:

```http
GET /api/v1/plm/sample-requests/{sampleRequestId}/history
```

The history endpoint treats the sample request and its product detail as one business record. It loads audit logs from `Audit.AuditLogs` where `SchemaName = SampleRequests` and:

```text
TableName = SampleRequests, RecordId = SampleRequest.SampleRequestId
TableName = Products,       RecordId = SampleRequest.ProductId
```

Rows with the same `CorrelationId` are grouped into one timeline item so a single save operation can show both `SampleRequests` fields and `Products` fields together. Each changed field keeps `details[].source` so the frontend can still show whether the field came from `SampleRequests` or `Products`. The response is scoped by the current user's `CompanyId`; users cannot read audit history for sample requests in another company.

`POST` creates a new attachment collection automatically. File upload still uses the attachment endpoints.

`PATCH` updates only fields included in the request body. It can update core sample request fields and a small set of product-detail fields used by the detail screen.

Production orders are still returned under `productionOrders` by linking:

```text
SampleRequest.ProductId -> MfgProductionOrder.ProductId
```

`MfgProductionOrders` does not own attachments in the current database schema, so do not add or query `manufacturing.MfgProductionOrders.attachment_collection_id`.

Do not key production order lookups by product code, colour code, or external-id snapshots. Use `ProductId` when both sides have it. Snapshot strings are display/fallback data only.

Each production order also returns the currently selected manufacturing formula header by linking:

```text
MfgProductionOrder.MfgProductionOrderId -> ProductionSelectVersion.MfgProductionOrderId
ProductionSelectVersion.ManufacturingFormulaId -> ManufacturingFormula
```

The selected manufacturing formula rule is:

```text
ProductionSelectVersion.ValidFrom IS NOT NULL
ProductionSelectVersion.ValidTo IS NULL
```

`productionOrders[].formulaExternalId` is populated from the selected manufacturing formula when one exists. If no selected manufacturing formula exists, it falls back to `MfgProductionOrder.FormulaExternalIdSnapshot`.

`productionOrders[].selectedManufacturingFormula` is a lightweight header only. It includes the manufacturing formula id, external id, name, total price, and active material count. It does not include manufacturing formula materials, so summary payloads stay small.

Manufacturing formula materials are lazy-loaded from:

```http
GET /api/v1/plm/manufacturing-formulas/{manufacturingFormulaId}/materials
```

`productionOrders[].standardManufacturingFormula` is loaded through:

```text
MfgProductionOrder.ProductId -> ProductStandardFormula.ProductId
```

The current standard rule is:

```text
ProductStandardFormula.ValidTo IS NULL
```

It returns the current standard formula header, the date it became valid, and the closest previous standard formula when one exists.

The selected customer formula header is returned under `selectedFormula` by linking:

```text
SampleRequest.FormulaId -> Formula
```

`selectedFormula` is `null` when `SampleRequest.FormulaId` is null or the formula is inactive. Summary does not return formula materials by default. It returns `materialCount` and `materialsUrl` so the frontend can lazy-load materials when a row is expanded.

Formula materials are loaded from:

```http
GET /api/v1/plm/formulas/{formulaId}/materials
```

Formula materials are ordered by `LineNo`, then `FormulaMaterialId`.

## Query Organization

`GetSampleRequestSummaryQueryHandler` should stay focused on query orchestration.

Keep query-only projection models in:

```text
Queries/GetSampleRequestSummary/Models/GetSampleRequestSummaryModels.cs
```

Keep query-specific data loading in:

```text
Queries/GetSampleRequestSummary/Services/SampleRequestSummaryRelatedDataLoader.cs
```

This loader is static and receives `IPLMReadDbContext` from the handler. It is not registered in DI because it is a query-local helper without state.

Keep reusable helpers out of the handler:

```text
SampleRequestAdditiveHelper.ResolveGroupCode(...)
AttachmentFileHelper.IsImageFile(...)
AttachmentFileHelper.BuildUrl(...)
```
