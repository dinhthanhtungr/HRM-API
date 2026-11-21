# Development performance comparison

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
