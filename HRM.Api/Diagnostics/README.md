# Development API timing

In Development, both Debug and Release builds return `Server-Timing` on responses
under `/api`, including error responses. Endpoints with their own metrics keep
their original header. Otherwise `app_total;dur=...` reports milliseconds from
entry into the application pipeline until response headers start. For downloads
and streaming responses this excludes the subsequent body transfer. Inspect the
response in the browser Network panel. No probe configuration is required.

Successful authenticated API calls also report completed middleware phases,
`dispatch_to_action` (including model binding), `mvc_action`, and the interval
from the end of the action to the start of the headers (`action_to_headers`).
`feature_<request type>` measures each MediatR operation. The same operation
called repeatedly is aggregated. Completed EF commands on ApplicationDbContext
appear in completion order as `db_command_1` through `db_command_12`, with later
commands grouped under `db_commands_remaining`. These durations exclude subsequent
reader consumption. No SQL text, arguments or row values are captured in headers.
Module-specific steps and existing totals are preserved. Feature, action, DB and
module timings can overlap or nest, so their sum is not request latency.

### Reading a feature across the API

In the Network Timing tab, `mvc_action` identifies the controller and action.
For example, the employee lookup reports `step_GetEmployeeLookupQueryHandler_ToPagedResultAsync`.
Inside it the shared pagination code times `totalCount_CountAsync` and `items_ToListAsync`
as separate steps. The notification publish step appears only when that branch actually calls
the notification service. Repeated calls to the same labeled step accumulate duration.

At build time the Roslyn generator in `tools/RequestTiming.Generator` instruments eligible
calls in API controllers, Application features and shared code, and Infrastructure. It uses
compiler symbols for labels, including the enclosing operation, result variable when available
and called method. It covers immediately awaited Task/ValueTask calls, forwarded business
Tasks, synchronous business calls with accessible signatures, and PDF/Excel rendering calls.
Forwarded Tasks retain their identity and completion behavior. Private helper bodies can contribute their own outgoing
steps. Local CPU-only code, deferred query expressions, queued work after the response, and
operations still running when headers start have no individual duration. Methods using
explicit RequestTiming steps retain those measurements.

The generated calls forward directly when no request is collecting timings. Development
only publishes details on authenticated successful responses. A roughly 4 KiB budget for
additional header content prioritizes the slowest steps when a request touches many call
sites. `steps_omitted` gives the number left out. For those requests, the Development logger
`HRM.Api.Diagnostics.FeatureSteps` at Debug level includes the full list of measured steps.

## Isolated performance comparison

`PerformanceProbe__Enabled=true` enables an isolated read-only comparison host.
It is off by default and startup fails if enabled outside Development. Debug and
Release comparisons must use the same Development configuration, user, query,
current source build, worker isolation and request order. Separate first-request
cost from warm runs. Do not compare a 302/401 baseline with an authenticated query.

Before starting the probe, `Prepare` removes only HRM application `IHostedService`
registrations, including factory registrations. Framework web hosting is retained.
Thus comparison hosts do not process notification/outbox/media workers. Do not use
this mode for ordinary interactive operation: non-read requests are rejected with
405, except POST login/refresh needed for the tester's normal authenticated session.
Only explicitly selected read endpoints should be benchmarked.

Authentication, current user, company scope, ownership/capability and field masking
are unchanged. Never extract browser cookies/tokens or bypass access validation.
The tester logs in normally in an isolated browser; measurements return only
status, duration, response byte count and selected diagnostic headers.

`X-HRM-Probe-Build` identifies compiled Debug/Release; `X-HRM-Probe-Debugger`
identifies actual attachment. A Debug binary alone does not prove a debugger is
attached. Successful authenticated responses append fixed `Server-Timing` metrics:
`probe_cors`, `probe_authentication`, `probe_acting_user`, `probe_authorization`,
`probe_handler`, `probe_total`. The handler phase includes downstream work up to
response headers, including serialization where applicable; it is not DB-only time.
The phase metrics partition elapsed time; do not add `probe_total` to their sum.
No SQL, query values, IDs, payload, account details or credentials enter metrics.

Bind isolated hosts to loopback, disable launch-browser startup actions, use
separate build outputs and stop only comparison processes when finished. Do not
restart or detach the user's existing Visual Studio process without coordination.
