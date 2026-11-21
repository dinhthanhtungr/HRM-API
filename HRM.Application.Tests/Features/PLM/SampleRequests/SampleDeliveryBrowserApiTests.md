# Browser integration QA for sample delivery options

`SampleDeliveryBrowserApiTests` is an opt-in local test host, not an application environment or deployment.

Run from the primary BE repository:

```powershell
$env:HRM_SAMPLE_DELIVERY_BROWSER_QA = '1'
dotnet test HRM.Application.Tests/HRM.Application.Tests.csproj --filter FullyQualifiedName~SampleDeliveryBrowserApiTests
```

The existing `IsolatedGroupStoragePostgres` fixture starts a new loopback cluster on a free port. Every connection verifies its data directory, database and user before any schema or data command. Synthetic tables use the existing EF mapping; this does not validate all production indexes/FK constraints. No migration, production connection string, application startup or notification delivery worker is used.

The host links the production `SampleRequestsController`, registers the actual MediatR handlers and uses EF/Npgsql. It listens only on `127.0.0.1:5318`; port conflicts fail rather than stopping another process. Synthetic identities use the test-only `X-QA-Role` header (`LabUser`, `SaleUser`, `ForeignSale`); no header returns 401. Visibility uses a test company scope. This verifies HTTP contracts and business capabilities, not JWT login or the complete production visibility implementation. CORS permits only the QA FE at `http://127.0.0.1:4318`.

`GET /qa/state` exposes only synthetic verification counts/results. After browser QA, call `POST /qa/stop`; the test asserts three trials with exactly one approved and two failed plus an audit, then the fixture stops its cluster. A 25-minute timeout also tears the host down. Without the opt-in environment variable this test is skipped.

## Verified 2026-11-14

- Actual FE components, React Query hook, use cases and repository called this HTTP API. A QA transport restricted requests to this host and supplied synthetic identity; it did not run the production login/refresh flow.
- Lab sent three formulas with 2/3/4 kg: three waiting trials, one batch and one outbox item.
- Sale approved option 2; siblings became failed only after the response/refetch and all saved controls required the pencil to edit.
- A second HTTP session switched approval to option 3; replaying the same operation did not add events, audit, CRM or outbox items.
- Saving a stale browser draft was rejected; the draft stayed intact and test DB counts did not change. Reloading and editing with fresh tokens successfully selected option 1.
- Replaying the original send returned 200 without additional trials. Changing its quantities returned 400. Anonymous GET returned 401; foreign company GET 404; Sale send and Lab feedback 400. All rejected/replayed requests left persisted state unchanged.
- Final persisted state: three trials, exactly one approved, two failed, three feedback audits, three CRM interactions and four outbox entries (one send plus three feedback operations). No delivery worker processed them.
- Browser row positions/heights stayed unchanged through a successful save; a 390 px viewport had no horizontal overflow.

Browser QA test result: `HRM.Application.Tests/TestResults/sample-delivery-browser-api.trx` (one passed). The test-only FE harness resides in the Codex visualization directory; it is not an application route.
