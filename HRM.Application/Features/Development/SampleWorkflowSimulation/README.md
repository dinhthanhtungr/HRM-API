# Developer sample workflow simulator

Customer price feedback is request-level. Starting another formula preserves the latest accepted price unless an explicit price outcome revokes or replaces it. The production workflow rules are reused; selected sample options and exact Sale-requested Lab finalization still retain their own formula identity.

Lab FinalizeQuotation/FinalizeChip now completes the synthetic request as well as
its exact Sale-requested formula. The evaluator reuses SaleRequestedFinalizationRules
for request identity, price/chip acceptance and unfinished delivery. No subsequent Sale
Complete action is offered. The completed quote/chip scenarios start closed; legacy
open/order-ready evidence remains supported by production flow rules. Kg option feedback
keeps its separate automatic-completion path. No production commands or DB writes occur.

Both quotation and chip finalization requests are Outstanding actions labeled
Yêu cầu chốt công thức. Each opens its note form directly and hands work to Lab;
the change does not grant finalization outside production allowed actions.

Accepted kg feedback automatically completes the synthetic formula and request, matching
production single-Trial and delivery-option feedback. Other completion actions still
use production allowed actions, including required customer price acceptance.
Open Sale cases include Complete only when production CompletionVisible allows it:
quotation-only cases hide it until Lab finalizes the formula; other cases keep the
disabled control with production CompletionBlockers until allowed. RequestLabFinalization
is Outstanding work and assigns Lab finalization before Sale completion.
Evaluator rejects disabled actions. Main action priority is price, kg,
then chips; navigation decisions never become pending work. Formula-finalization requests
remain separate actions and are styled green; cancellation remains red and reasoned.
Quotation + chips requires accepted price and chip feedback. Saving an accepted kg
sample (with or without chips) completes immediately, without a separate Complete action
or waiting for price/chip feedback. One option wins; the other options in its batch fail.
Outstanding price and required delivery feedback are both marked as main work in FE.

## API

- `GET /api/v1/dev/sample-workflow-simulator` returns synthetic scenarios.
- `POST /api/v1/dev/sample-workflow-simulator/evaluate` evaluates initial state or applies a simulated action to a client-carried state.

Both routes require authenticated Developer access; handlers independently require Developer and a company context. Lab/Sale in the payload is a simulated audience, never an impersonated identity. There are no real entity IDs, employee resolution, persistence abstractions, DB queries, migrations, file writes, notification jobs or production PLM mutation calls in this feature. The API may be used wherever the existing Developer tools are deployed; it is not gated only by ASPNETCORE_ENVIRONMENT.

## Ownership

- `BusinessSimulationCatalog.cs`: ordered public business scenarios in Quotation / Chip / Sample / ChipThenSample groups. Initial confirmation, standard-price approval, quotation/price responses, delivery responses, rework, finalization, completion and cancellation are separate milestones. Parallel delivery/pricing cases are explicit.
- `BusinessSimulation.cs`: constructs synthetic request/formula/trials/events and calls the **production** `SampleFlowStateReader.ResolveDecision` and `Build`. The production assignment, stage, allowed flow/plan choices and pending-trial guards determine visible actions for the simulated responsible Sale/Lab. Commands remain in-memory and never call production mutations. Delivery history keeps separate trial identities so chip-then-kg evidence is not lost.
- `SimulationCatalog.cs`: public catalog router plus private-picker regression fixtures. Existing technical case IDs remain evaluable for automated form/permission regressions, but GET no longer lists them.
- `SimulationModels.cs`: bounded state and public DTO contract; synthetic product/request context.
- `SimulationDecisions.cs`: legacy technical fixture evaluator only; not used for public business cases.
- `SimulationTransitions.cs`: input validation and pure state transitions. A request is stateless and cannot mutate another user's simulation or application data.
- Query/command handlers enforce endpoint eligibility. Controller only dispatches and maps forbidden/invalid requests.

The frontend route is `/dev/sample-workflow-simulator`. Cases are not duplicated in FE. New field kinds/actions may require UI rendering support; adding another case using existing kinds requires only BE changes.

## Business interaction and limits

FE displays completion immediately above cancellation in Other actions, independently of its compatibility
Group value in the simulation API. Supplementary requests contain only additional work.
Choice descriptions are concise in Vietnamese and English; permissions and reason
validation are unchanged.

The default is `quote-new`. Switching Sale/Lab retains the synthetic session; selecting a scenario or Reset loads its initial evidence. `WorkCode`, `WorkRole` and `Stage` expose production routing, while `Workflow` carries bounded synthetic evidence. FE shows formula confirmation/finalization, standard price, customer price response and latest delivery independently. Selecting an action and saving recomputes the next work assignment; feedback never automatically completes a request. Customer cancellation and final completion close the request. Supplementary choices come from the current production plan rules. RequestRequote records its synthetic notification/history only, preserving the live command's behavior rather than revoking accepted price or reopening completed work.

Audience actions are independent of the canonical main assignment: Sale's pending quotation/price response and chip/kg feedback remain Outstanding while Lab prepares/sends delivery. PrimaryAction is chosen from the current audience's available Outstanding actions even when WorkRole is Lab. PendingSale includes both feedback tasks when concurrent, consistently in either audience. FE exposes each pending feedback action directly on the card. Accepted/rejected price responses do not stay pending unless a newer pricing approval opens another response cycle.

`PendingDeliveryPlan` describes the outstanding synthetic delivery plan and relevant quantities. Re-selecting exactly the same pending type/quantities is rejected with `SimulationPlanUnchanged` before events/history are appended; changing only the note does not create a plan revision. Chip-then-kg remains pending between its chip and kg delivery. A subsequent supplementary request after fulfillment/completion is not blocked merely because it uses the same type/quantities. FE disables unchanged submissions using the server snapshot; evaluator rechecks independently. This guard is simulator-only.

Management approval is deliberately a separate Developer simulation control (`ExternalActions`), never a Sale/Lab workflow action. It advances synthetic pricing evidence without granting either role management permissions. Real company/ownership/field-visibility eligibility still belongs to production APIs; simulated audiences assume the responsible authorized Sale/Lab, not a selected real employee.

Presentation grouping: a plan-only choice is `Supplementary` and appears in the collapsed section below a divider. A choice also present in the current flow's AllowedActions stays in the main decision group. This changes presentation only, not availability or transitions.

**Approved simulator-only exception:** Sale always has CancelRejectedRequest in Supplementary for every synthetic request not already cancelled (including completed seeds). A reason is mandatory, recorded in synthetic history, and cancellation closes the synthetic request. FE places cancellation last. This is a prototype proposal requested explicitly by the user; production cancellation eligibility, UI and workflow remain unchanged until separately authorized. Other assignment/choice rules continue to use production PLM routing.

**Simulator-only Lab rejection:** open synthetic requests expose `RejectFormula` to Lab in `LabOther`. A mandatory reason sets the request to Rejected (clearly labeled Lab formula rejection), pauses all regular work and creates a synthetic General-group notification. Only Sale can `ResumeAfterLabRejection`, also with a mandatory new reason; it restores Open and recomputes the pre-existing workflow without confirming/replacing the formula, changing the plan, delivery or price acceptance. Both transitions append reasoned history and explicit Lab/Sale General notifications. Sale can instead cancel under the existing simulator proposal. Completed/cancelled requests cannot be rejected by Lab. These custom simulation events are ignored by production routing, and no production notification/mutation API is called. Notification payloads are bounded and rendered only as simulation records.

The catalog covers standard price absent/present, not quoted/waiting/accepted/rejected price, repricing, delivery with concurrent pricing outstanding, accepted/failed/cancelled delivery, Lab finalization, Sale completion and supplementary work. Labels are bilingual. Technical permission-only contexts remain automated fixtures and are not business stages.

## Legacy regression fixture behavior (not public business workflow)

Actions now include server-owned Group/Enabled/DisabledReasons for a navigation-only chooser (Outstanding, Other, Final, Supplementary; legacy Internal actions are hidden by FE). Processing happens only after explicit Save/Confirm. Complete remains in the list when blocked, and the evaluator independently rejects disabled actions. Completion follows actual requested work: price only, price+chip, or price+kg; kg feedback supersedes mandatory chip feedback. Any response records a batch response; only an approved option satisfies sample completion.

`SimulationInput.Feedbacks` atomically applies 1–30 distinct validated option changes. All IDs/results/dates/notes and the at-most-one winner rule are checked first; a winner fails siblings in the same batch. The legacy single-option input remains supported. Old-batch ambiguity is cleared only when every outstanding batch actually has a response, not by silently requiring every option.

Both simulated roles may Reject with a reason and Resume; cancellation remains Sale-only. History retains translated action titles, reasons and status transitions. Completed requests expose direct supplementary request actions; these archive the completed batches before starting new work. The former Supplement action remains Internal for compatibility. All state is still client-carried synthetic data, with bounded history, no persistence or schema changes.

Formula candidates expose Confirmed. The first delivered option must be confirmed according to the server catalog; additional drafts can be included without auto-confirmation. This is independent of the caller's supplied confirmation flag.

`SimulationView.Quotation` is catalog-owned synthetic context: quote/acceptance/history dates, restricted-content flags, linked references, date capability, customer-price capability and the same workflow fallback inputs consumed by the live form. Six additional catalog cases exercise linked, locked, read-only, date-only, repriced and restricted-content views. `ViewPriceFeedback` permits reviewing accepted/rejected price state; it does not grant overwrite rights. The evaluate endpoint independently rechecks date/feedback permissions, linked-reference membership, unchanged accepted feedback and required public notes. Neither real roles nor row capabilities can be expanded by the simulated audience or this fixture context.

The optional `QuotationFeedback` draft in simulator input/state retains sent date, response date, outcome, customer note and separate price-exchange note for the real price-form presentation. Validation bounds all fields, checks date ordering and requires customer content when recording feedback. It never creates a CRM interaction, updates an actual quotation, or delivers a notification; those labels belong to the shared live UI. Existing legacy simulation result payloads remain supported. A new quotation request clears the previous price draft.

Pending chip/kg/quotation requests do not reappear as duplicate request actions. Outstanding Sale feedback is separate and survives new requests/deliveries. A combined plan sends chips before kg without requiring chip feedback. Per-option approval fails siblings only within its batch; editing a recorded result requires the pencil in the UI.

Completion and cancellation are simulated only. Supplementary work archives completed batch results in the returned state. Missing old feedback and an unresolved old-batch completion policy are visible. Adding chips to an already pending kg plan is marked unresolved and cannot be applied. This feature intentionally does not replace production PLM rules or infer management pricing approvals.

## Tests

`SampleWorkflowSimulationTests`: all case/audience decisions, transitions, authorization, bounded inputs and fixture isolation. `SampleWorkflowSimulationHttpTests`: actual linked controller over HTTP, anonymous 401, non-Developer 403, Developer catalog/evaluation and invalid action 400. The test host has no DbContext registration.

For optional isolated browser QA, set `HRM_SIMULATOR_BROWSER_QA=1` only for the test process and run that HTTP test. It binds loopback 5319, allows CORS only from the QA UI on loopback 4319, and waits for `POST /qa/stop` or a 25-minute timeout. Its synthetic authentication handler exists only in the test assembly. Normal automated runs use an ephemeral loopback port and stop immediately.
