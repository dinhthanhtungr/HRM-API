# Legacy TP follow-up HTTP diagnosis

Run `dotnet test HRM.Application.Tests/HRM.Application.Tests.csproj --filter FullyQualifiedName~LegacyFollowUpHttpTests --logger "console;verbosity=normal"`.

Uses production SampleRequestsController and FormulasController, MediatR handlers and EF/Npgsql.
The guarded IsolatedGroupStoragePostgres fixture creates a fresh localhost-only cluster;
the HTTP host uses an ephemeral localhost port and clears configuration sources. It does
not launch production Program, read production settings or start notification workers.
Synthetic identities replace JWT login; customer visibility is scoped by the QA adapter.
Only outbound SendSampleRequestMessage is intercepted. This does not validate message
delivery, production login, complete production schema constraints or browser rendering.

Synthetic completed TP records were created on 2026-10-31. Verified 2026-11-26:

| Flow | HTTP observation | Persisted result |
| --- | --- | --- |
| Request chip, Lab send, Sale approve | 200 at all three steps | New trial, workflow events, TP InProgress pending remaining conditions |
| Request kg, Lab send, Sale batch approve | 200 at all three steps | TP Completed, selected FormulaId equals approved option |
| Request requote | 200 | Same TP reopened InProgress, activation marker and requote event, no trial created |
| Quote feedback NoResponse without approved standard price | 400, generic unavailable-action message | Feedback not accepted |
| Same quote feedback with approved VND standard price | 200 | Price feedback stored through production handlers |
| Requote with Approved VU but no confirmation event/version/message | 400, generic unavailable-action message | No partial workflow/token write |

CreatedDate remains unchanged. Anonymous mutation returns 401. Only test harness files
were changed; no business implementation was fixed. Pricing approval/publication and
Accepted price feedback are not exercised: the approved price and existing quote date
are explicitly seeded into the test database. Chip formula finalization is not exercised.

Result log: `artifacts/legacy-followup-http.log`. Existing follow-up rule/PostgreSQL tests
also cover history preservation and an old unlinked Approved VU selected for delivery.
