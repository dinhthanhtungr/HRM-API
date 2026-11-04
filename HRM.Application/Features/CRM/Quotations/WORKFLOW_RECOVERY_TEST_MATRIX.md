# CRM Quotations: workflow recovery verification

## Scope and invariants

This matrix covers the five approved recovery changes. It is a finite partition of user-visible behaviors,
not a claim that every decimal, timing interleaving or production dataset has been exercised.
Authorization, company/customer scope, immutable Sent pricing, and optimistic concurrency remain enforced.
No migration, backfill or historical price reconstruction is performed.

| Area | Automated coverage | Expected outcome |
| --- | --- | --- |
| Status 0/10/20/90/30 and terminal/unknown states | FE permission matrix; BE withdrawal status matrix | Save/edit only in allowed states, withdraw-send only at 90, Sent locked |
| Inside/crossing tiers, unequal/equal overlaps, touching boundaries | BE comparison tests; FE send-price tests | Longest positive overlap, then higher price on tie |
| Gap, single inclusive/exclusive point, outside, unbounded ranges | BE comparison and FE reference tests | Without positive overlap use boundary/adjacent fallback; after-table uses last quantity tier, before-table uses global highest |
| Negative/zero/inactive/missing standard tiers | Existing BE comparison tests and FE metadata tests | Invalid/absent standard fails; approved zero is not missing |
| Below/equal/above, commission, six-place persistence rounding | BE comparison/workflow tests, FE send tests | Only below needs a reason/board confirmation |
| Currency and expiry/future approval boundaries | BE PricingFreshness tests; FE metadata/currency tests | Matching currency/effective approved table; review warnings alone do not prohibit sending |
| Pending save with changed prices, commission, ranges/toggle/note | BE Revision tests | Supersede old snapshot, request new confirmation or return Approved; never infer Sent |
| Revision reason empty/4001, multiline, cancel, stale preview | FE DOM interaction tests and wire tests; BE existing reason guards | No silent save; reason dialog is available, user input retained, server guard wins |
| Withdrawal with valid/expired/missing standard, legacy/no request | BE Withdraw tests; FE DOM missing-standard scenario | Return Draft without changing customer prices; supersede old requests |
| Withdrawal input, token, inactive/foreign company, nonpending status | BE Withdraw tests; FE use-case/HTTP matrix | Reject without publication; never use current prices to rebuild history |
| Concurrent withdrawal/confirm; stale/repeated save; DB conflict | BE Withdraw and Revision tests | One valid transition; transaction rollback on failure |
| Confirm permission and scoped wrong message/company | Existing BE workflow and InternalMail tests | Only authorized current viewer can confirm a valid pending request |
| Rule version 0/1/2 and changed/expired standard | BE PricingFreshness/CommercialApproval tests | Current customer offer can be confirmed without rebuilding historical Body/comparison; standard changes alone do not revoke approval |
| Customer prices/commission/ranges/terms change, even with unchanged total | BE CommercialApproval tests and InternalMail tests | Reject stale commercial offer consistently in detail/messages/POST; retain permission/token/transaction guards |
| Unchanged pending save after standard disappears | BE CommercialApproval tests | Preserve pending request/token and customer prices without repricing |
| Superseded history | FE parser/action-card tests | Show history/status with no confirm action |
| HTTP 400/401/403/409/500/503, missing save token | FE wire tests; DOM save/withdraw error scenarios | Preserve errors; no automatic mutation retry |
| Save committed then canonical GET fails | FE DOM interaction test | Keep draft, lock duplicate mutation, explicit canonical review |
| Realtime bursts, dirty input, mutation race, disposal | FE LiveRefresh tests and screen DOM scenario | Debounce clean refresh, retain dirty input, explicit discard/reload |

## Test implementations and commands

- BE: `QuotationSendPriceComparisonTests`, `QuotationSendWorkflowTests` including Revision, PricingFreshness,
  Withdraw partial files; `InternalMailAreaAccessTests`.
- FE: module Quotations tests, notification parser/presentation/API/card tests.
- FE DOM: `QuotationDetailScreen.workflow.test.tsx` uses the real screen, mapper, schema, resource and workflow
  dialogs with mocked API, UI primitives and a deterministic form shell. It checks orchestration, not full widget styling.
- FE dependency `jsdom` is a development-only DOM test runtime.

```powershell
dotnet test HRM.Application.Tests/HRM.Application.Tests.csproj --no-restore --filter "FullyQualifiedName~QuotationSend|FullyQualifiedName~InternalMailAreaAccessTests" -m:1 --disable-build-servers
dotnet build HRM.Api/HRM.Api.csproj -p:OutDir=../artifacts/verify-build/ --no-restore -m:1 --disable-build-servers
```

From the FE repository:

```powershell
node node_modules/vitest/vitest.mjs run src/modules/crm/quotations src/core/notifications/domain/quotationSendConfirmation.test.ts src/core/notifications/domain/quotationConfirmationPresentation.test.ts src/core/notifications/infrastructure/api/QuotationSendConfirmationApiRepository.test.ts src/shared/ui/Layouts/AppShell/notifications/QuotationSendConfirmationCard.test.tsx
node node_modules/typescript/bin/tsc --noEmit --incremental false
npm.cmd run build
```

## Live integration checks requiring a dedicated environment

Automated store/mocked HTTP tests do not prove PostgreSQL rollback/outbox delivery or real browser layout.
Use disposable quotations in a named test company; do not send test notifications to production employees.

1. Sale edits every supported tier field at 10/20/90, saves below/equal/above with commission, cancels reason,
   exports PDF after saving, and reloads to compare canonical tokens and prices.
2. President opens the old request while Sale saves/withdraws. Attempt both actions concurrently from separate
   processes; verify only one transition, no duplicate interaction and all old notifications resolved.
3. Expire/remove the approved standard or pending request; withdraw at 90, request pricing again from Draft,
   and confirm that customer snapshots/history are retained.
4. Disconnect after commit but before response, fail canonical GET, then restore network. Verify explicit review
   prevents duplicate mutations and reload presents the committed state.
5. Trigger realtime while editing price/range/note and during save; verify no lost input. Check desktop/mobile,
   dialog keyboard focus, disabled explanations and discard confirmation.
6. Exercise anonymous, foreign company/customer scope, inactive employee and revoked capability through real
   HTTP authentication. Unit fixtures do not emulate the complete authentication middleware.


## Verification results (2026-10-19)

- Sending/withdrawal and InternalMail targeted suite: **125 passed**, no failures/skips.
- Broader Quotations plus InternalMail suite: **274 passed, 2 failed** (276 total).
  The failing assertions are in unchanged tests/implementation outside these five changes:
  `ProductPricingHealthEvaluatorTests.Evaluate_MissingMaterialPriceIsWarningOnlyAndKeepsReviewDueDate`
  (expected MissingMaterialPrice, actual PendingReapproval, line 29), and
  `ProductPricingVersionPolicyRulesTests.Create_UsesRealtimeSnapshotAndPolicyGeneratedTiers`
  (expected 156, actual 163, line 45). Neither execution path uses the send comparison/withdrawal service.
  Do not silently change pricing/review policy just to make these assertions pass.
- FE targeted suites: **139 passed** across 12 files, including **17 DOM interaction cases**.
- API build, FE production build, standalone TypeScript and targeted production-file ESLint passed.
- Existing .NET ImageSharp NU1902/NU1903 dependency warnings remain; this task does not change that dependency.
- Full authenticated browser/PostgreSQL/outbox checks above have not been run against a dedicated environment.
  DOM tests use fake API/form shell and do not establish full widget behavior or live multi-process transactions.
