# Production order Inform editor

## Routes and security

- `GET /api/v1/plm/production-orders/{id}/inform`: existing `workspace` and
  `selectedFormula` remain; adds `revision`, `allowedActions`, `editableFields`,
  `canEditItems`, `readOnlyReason`, `stepOptions`, `formulaSources`.
- `POST /api/v1/plm/production-orders/{id}/actions`: unified editor mutation.
  `Save`, `UseStock` and `SaveAndCreateFormula` optionally include `deliveryPromises` (order/detail IDs, schedule revision,
  changed schedule IDs and nullable promised dates). The Inform response advertises
  `supportsDeliveryPromisesSave: true`; older responses must not enable this editor.
  Header/VA changes and promised dates commit in the same MFG transaction, with
  schedule capability, scope and concurrency rechecked. No separate schedule write
  request or nested commit. Omitted promises preserve the existing single-date flow.
  A New MFG retains its existing Cancel / Request formula / Request stock controls;
  it does not gain a standalone Save button. Each existing submission carries its
  promised-date draft into that writer's transaction.
  See [the MFG delivery-date contract](README.md#ngày-yêu-cầu-theo-đợt-trong-mfg).
  Action `RollbackUseStock` accepts only action, revision and empty clearFields.
  It requires the Inform operator role and production-order manage capability,
  active company-scoped sale link and EmployeeId. After locking the MFG it checks
  revision and Stocked status again. Any VA selection history, schedule, material
  request link or production-output source blocks rollback. Otherwise it changes
  Stocked to New, updates audit and writes timeline in the same transaction.
  Planning/notes, original CreatedDate and sales-order status remain unchanged;
  it does not delete VA/history, release reservations or reverse physical stock.
  No migration/backfill. Example: `{ "action": "RollbackUseStock", "revision":
  "64-character token returned by Inform", "clearFields": [] }`.
- `GET /api/v1/plm/production-orders/step-options`: authenticated company and
  production-order view or create capability; used by creation forms.

All routes retain Authorize. Actions require current company, EmployeeId and an
active company-scoped sale-order/detail link. Order managers retain existing actions.
Formula editors (`plm.manufacturing-formula.manage`, existing Developer/LabUser
mapping) may Save formula items and QC/notes, or SaveAndCreateFormula for New orders.
They also need production-order view; material edits require the separate material
capability. Prices retain their existing field masks.

For formula-only editors, `editableFields` is labNote, qcCheck; `canEditItems` depends on material visibility. Header fields (quantity,
batches, step, packaging and dates) remain readonly. The actions endpoint rejects
supplied header values/clears even if equal to the stored value. Underlying Inform
and formula-creation writers recheck unchanged header values against the locked
company-scoped order, so direct calls cannot bypass this restriction. UseStock,
RequestFormula and Schedule require order-management capability; Check/Finish keep
their existing permissions. Source/status rules and creation side effects remain.
LabUser editor Save in FormulaRequested completes the currently selected active VA
even when materials are unchanged: the server locks and resolves the open selection,
requires formula-management/material capabilities and nonempty active materials with
nonnegative quantities and at least one positive quantity. VA becomes Checking,
MFG becomes FormulaSuccess, and audit/timeline and the existing price warning run
in the same transaction. Missing/foreign/inactive selection or stale revision fails.
This Save does not create a schedule or advance to Scheduling, including Lab+PLPU
accounts. A later PLPU Save from FormulaSuccess performs the existing Scheduling,
schedule creation and reservation synchronization. A repeated Lab-only Save preserves
FormulaSuccess without another completion event. Other formula-only saves preserve
status and do not create a missing schedule. Existing schedule notes and material reservations still sync.
Formula editing reuses the existing capability and seeded LabUser claim.
PLPU note belongs to order management (`plm.production-order.manage`), not formula
editing. Formula-only/Lab users see it readonly; actions rejects posting/clearing
plpuNote and underlying writers reject a changed manual value. An omitted or
unchanged forwarded value preserves the stored note. Material edits still generate
the server-owned AUTO_PLPU_NOTE block normally; this rule does not suppress that
audit trail or grant Lab permission to type into the PLPU field. No new capability,
role claims or schema changes are required for this PLPU note restriction.
The customer Requirement is readonly unless `plm.production-order.requirement.edit`
is granted (seed/fallback: Developer only), independent of order-management rights.
Inform omits requirement from editableFields without that capability. Actions rejects
posting/clearing it; underlying writers reject changed values while accepting the
unchanged stored value forwarded by the adapter. This includes Inform, Information,
UseStock and CreateFormula routes. Baseline forward/rollback role-claim SQL includes
the new Developer permission; no schema migration or DB seed has been executed. If an explicit token lacks that claim, re-login/refresh
after the claim is granted; the editor does not bypass an explicit permission set.
Client-supplied category, price, snapshot and target status are not trusted.

```json
{
  "action": "Save",
  "revision": "64-character token returned by GET",
  "labNote": "Operator note",
  "clearFields": ["expectedDate"]
}
```

Omitted fields remain unchanged. Explicit clearing uses `clearFields`, not null.
Clearable fields: stepOfProduct, expectedDate, manufacturingDate, bagType, labNote,
plpuNote, requirement, qcCheck. Conflicting value/clear, unknown and duplicate
clear fields are rejected. Quantity and batches, when supplied, must be positive.
Unknown stored process values survive a header-only save; new values must be valid.

For `Save`, `selectedFormulaId` and `items` must be sent together when changing
the selected VA. Server verifies the open selection, resolves references, lot,
unit/category/price, generates material-change notes and replaces items without
creating a formula snapshot, preserving legacy Inform behavior.

`SaveAndCreateFormula` requires sourceFormulaId, origin and items, and resolves its
source against server options. Only FromVu (first production) creates an
effective VA (Checking), selection ValidFrom=now and Scheduling order. The same
transaction creates/updates the schedule and synchronizes material reservations
from the server-resolved rows. This option returns requiresFormulaReview=false;
creation returns isSelectionEffective=true. Standard, Improvement, Production and ProductionOld
create a pending VA (New), pending selection (ValidFrom/ValidTo null) and
FormulaRequested order, without creating a schedule or reservation. Those options
return requiresFormulaReview=true; creation returns isSelectionEffective=false.
Lab/QC completion activates the pending selection before the later PLPU Scheduling
step. Origin is matched against company/product-scoped server options, never used
as a client-selected shortcut to skip QC. Reservations do not physically issue stock.
Per-row sourceItemId is optional tracking metadata:
missing/unknown IDs and repeated source IDs do not block creation. Unknown IDs never
load external rows. A unique same-item match within the validated source may retain
its price and saved lot; ambiguous/unmatched rows use validated current pricing and
lot checks. In every category, repeated items are allowed when quality type or lot
differs; only the same item ID + resolved quality type + normalized lot is duplicate.
When one lot exists in both good and defective stock, the requested item type selects
the matching warehouse quality. Quality is still validated against warehouse data.
For New MFG source/Inform reads, lotNo defaults to the oldest eligible good-stock
lot (min UpdatedDate, then min ShelfStockId), or good N/A when none exists.
The returned itemType is also the good Material/Product type; saved source lots
and defective quality do not become defaults. GET never persists this preview.
For non-New MFG reads, saved lot and quality remain unchanged. The separate Copy
command retains its existing behavior.
Depleted saved lots remain selectable with zero stock and Unknown availability;
their saved quality type is retained, rather than claiming current QC/stock.
Defective-lot visibility remains capability-gated. Creation rejects defective input
or resolved defective rows without that capability, including source-copy defaults.
Material-change notes use available scoped matches; unmatched rows are additions,
and unmatched original rows are deletions. Other writers retain strict source-ID
checks. No per-row source linkage is persisted on the new VA.
`UseStock` accepts optional sourceFormulaId solely
to merge the validated source note on the server. Neither accepts a target status.
Non-Save actions do not support clears or bagType; manufacturingDate is accepted
only by Save/Create. Save existing edits first if another action cannot carry them.

## Status, action and side effects

| Current state | Actions | Save behavior |
| --- | --- | --- |
| New | Save, RequestFormula, UseStock, SaveAndCreateFormula with material capability | Header/items; preserve New |
| FormulaRequested | Save | LabUser editor Save completes selected VA to Checking and MFG to FormulaSuccess; other saves preserve status |
| FormulaSuccess | Save, RequestFormula, Schedule | Save transitions to Scheduling |
| Scheduling, Scheduled, Waiting, Unassign, Getback, QCinprogress, QCPassed, QCFail, BTPNew, Weighting, Weighted, Mixing, Mixed, Started, Running, Change, Unassignfrommd, Reported | Save | Preserve status |
| Finished, Canceled, Done | Save | Preserve terminal status, do not reopen |
| Stocked | Save | Preserve stock branch; header-only saves do not reserve material |
| Unknown/unrecognized | None | Read-only with reason |

Save creates a missing schedule for any state except FormulaRequested and Stocked,
including terminal states, as legacy does. Existing schedules receive expected date,
notes, requirement and process. Quantity changes with an open VA, changes to VA
items and entering Scheduling synchronize reservations in the writer transaction.
Formula creation and stock actions reuse their existing sale-order, selection,
schedule, reservation and timeline rules. Stock use does not physically deduct
stock; its existing CreatedDate reset is retained.

Expected-date warning and workflow notification recipients/triggers remain those
of the existing writers. No new channel or recipient expansion was added.
Check/uncheck, Finish, colour adjustment and PDF remain separate operations.

## Concurrency and response

The revision hashes the MFG header, open selections and selected formula update
timestamps. GET rejects a changed aggregate observed while composing its result.
Each writer checks revision after locking MFG; VA editing checks again after the
formula lock. Stale requests fail without committing, requiring reload. This is
optimistic concurrency, not a durable idempotency-key/replay service.

POST returns existing OperationResult (success/message). The new FE then GETs
Inform again through cache invalidation and uses the saved canonical baseline.
Transaction ownership stays in each Application writer; controller only dispatches.

## Compatibility and process codes

Existing PATCH inform/information, POST actions/{action}, use-stock and formula
routes remain for existing consumers. Their historical payload semantics remain;
in particular raw PATCH information still accepts its old status contract and does
not become a sparse editor action. Existing endpoints without revision do not gain
the editor's concurrency guarantee. They reuse the same writers; removing or
changing these public contracts requires a separate consumer migration.

StepOfProduct now uses legacy codes 0–11. Codes 6–11 are MDTS_GH, BTS_GH,
CT_MD_HT_GH, MD_HT_GH, CT_HT_GH, CT_MDTS_GH. Previously the new enum's 6–8 had
different meanings. Existing rows cannot be classified reliably from the numeric
code alone. No database query, migration or backfill has been run. Provenance-based
classification and any data correction require a separately approved data task.

## Verification

Manufacturing tests cover status rules, enum codes, sparse clear/preserve semantics,
permissions/company/link checks, stale drafts, reservations and writer rollback.
FE tests cover sparse payloads, zero/unknown process values, draft guards and all
status editor visibility. SQL locking, actual notifications and legacy/new output
comparison still need a connected test database and authenticated integration run.

## Selected VA note

`POST /api/v1/plm/production-orders/{id}/actions`, action `Save`, accepts optional
`formulaNote` (max 20000 characters) with `selectedFormulaId`. This edits the active
selected ManufacturingFormula.Note, never the order LabNote/PlpuNote/Requirement.
Omitted/null means preserve; `clearFields: ["formulaNote"]` explicitly clears it
and cannot be combined with a value. Notes preserve line breaks. Other actions
reject this field. Inform advertises `formulaNote` only for a selected VA and the
existing formula-edit/material-view capabilities (Developer/LabUser fallback).
Note-only Save does not require items or resync stock. Supplying items edits both
atomically under the existing order/VA locks, company/link checks and revision.
VA UpdatedBy/UpdatedDate are updated, so subsequent stale saves conflict.
Example: `{ "action": "Save", "revision": "<current 64-char token>",
"selectedFormulaId": "<active VA id>", "formulaNote": "2 lưới\nChỉnh STD" }`.
No schema change is required. Deploy/restart the updated API before FE acceptance.
