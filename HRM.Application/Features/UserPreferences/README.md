# User preferences

Account-wide, non-business preferences owned by the authenticated Identity
account. Appearance = 1 retains its v1 color contract. Display = 2 and
CustomThemeLibrary = 3 add independent v1 groups without changing stored enum values.
Employee/company are not ownership keys here: accounts without an EmployeeId can
still choose their own appearance. No role capability is added or changed.

## Authorization and organization

All endpoints require Authorize. Handlers also fail closed for unauthenticated
users or Guid.Empty UserId. No userId, employeeId, companyId, roles or permissions
are accepted in the request. All store reads/conditional writes include the
authenticated UserId. A theme cannot grant field visibility or change business
status colors. Sample Work acting preview is not a source of preference ownership.

Controller -> separate MediatR list/detail/save handlers -> IUserPreferenceStore;
Infrastructure implements the store. AppearancePreferenceRules owns the supported
category mapping, defaults and payload validation. Adding another category requires
an enum value and its own contract/validator; do not accept arbitrary JSON groups.

## API

Advanced routes reuse the authorized conditional-read/write flow:
- `/api/v1/me/preferences/display`: colorScheme light/dark/system, density
  compact/standard/comfortable, textSize extra-small/small/standard/large, contrast standard/high,
  motion system/reduced/off, fontFamily open-sans/system/arial. Unknown/duplicate fields rejected.
  fontFamily is additive in schema 1: old five-field data remains accepted and normalizes
  to open-sans; the original five fields remain required. Font values are identifiers,
  never CSS, URLs or arbitrary font names. No change to ownership or revision handling.
  Defaults: light/standard/standard/standard/system, fontFamily open-sans.
- `/api/v1/me/preferences/custom-theme-library`: `{samples:[{id,name,preference}]}`;
  JSON response category is `customThemeLibrary`. At most 12 samples; unique nonblank
  IDs <=80 characters, trimmed names <=40, color preference follows Appearance v1
  without the browser's local version field. Empty library is the default.
- Each group has independent revision and server acknowledgement. Multi-group Save
  is not atomic; FE reports each group's pending/conflict/saved state.
- Sidebar pinned/peek state is not in server data. No new table, column, index,
  migration, SQL script or database write is part of this extension. Existing
  public.UserPreferences must match the declared configuration; runtime schema
  and database concurrency are not established by unit/build verification.

- GET /api/v1/me/preferences?categories=appearance returns an array.
- GET /api/v1/me/preferences/appearance returns one object.
- PUT /api/v1/me/preferences/appearance replaces the entire appearance group.
- Categories are stable lower-case names. Numeric or unsupported category routes
  return 400. Category stays an enum in the entity and DTO, integer in DB, named
  string in JSON. Never renumber/reuse persisted enum values.
- Reads allow 1–20 category names (duplicates are deduplicated by the current
  single-category reader); unsupported or empty names fail the whole request.
- Responses use Cache-Control no-store.

Example response:
```json
{
  "category": "appearance",
  "schemaVersion": 1,
  "data": { "preset": "default", "background": "neutral", "emphasis": "light" },
  "revision": 0,
  "updatedAtUtc": null
}
```

Revision 0 and null update time mean no persisted record: these are system
defaults, not a newly inserted row. Reads never create rows. Stored rows have
positive revision and a server UTC timestamp for their latest successful write.
SchemaVersion describes the JSON structure, not its update count.

PUT body:
```json
{
  "schemaVersion": 1,
  "expectedRevision": 0,
  "data": { "preset": "forest", "background": "neutral", "emphasis": "light" }
}
```

The response is the stored canonical object with incremented revision. Creation
requires expectedRevision 0. Updates compare account/category/revision in one SQL
statement; a unique-key collision during concurrent creation is also a conflict.
409 returns code preference_conflict, without silently overwriting another writer.
A lost acknowledgement can be recovered by reading and comparing the current data.

SchemaVersion must be 1. Presets: default/ocean/forest/violet/rose/slate/custom.
Only custom accepts color, exactly six hex digits prefixed by #, normalized lower
case. Background is neutral/tinted; emphasis is light/medium/strong. Unknown or
duplicate data properties, malformed values, negative/overflowing revision and
data larger than 16 KiB are rejected. PUT request limit is 20 KiB.
Validation returns 400 with code invalid_preference; missing identity returns 401.
A not-yet-created preference table returns 503 preferences_unavailable so FE keeps
its local theme. Other DB failures are not misreported as a missing deployment.

## Required database change — NOT applied

Add public.UserPreferences through the approved deployment procedure:

| Column | PostgreSQL type | Null |
| --- | --- | --- |
| UserId | uuid | no |
| Category | integer | no |
| SchemaVersion | integer | no |
| Data | jsonb | no |
| Revision | bigint | no |
| UpdatedAtUtc | timestamp with time zone | no |

- PK: (UserId, Category); this also supplies the account/category lookup index.
- FK: UserId -> public.AspNetUsers.Id, ON DELETE CASCADE.
- Checks: SchemaVersion >= 1, Revision >= 1, jsonb_typeof(Data) = 'object'.
- No separate Id, EmployeeId, CompanyId, duplicate UserId index or JSON search
  index. Version/revision/time are supplied by the application on write.
- EF configuration is auto-discovered; DbSet is in the context partial.
- No migration, schema script, backfill, seed or DB write was created/run.
- Before enabling durable server saves, provision this table and constraints,
  deploy the BE API, then verify read/create/conditional update with isolated test
  accounts. Existing users need no backfill; default responses cover absent rows.

## Verification limits

Unit tests cover identity scope, defaults without writes, validation, enum JSON,
conflicts, unavailable deployment and EF key/FK/type/index metadata. Persistence
round trips and concurrent SQL writes must be verified against an isolated DB
after schema provisioning; fake-store tests do not establish DB runtime behavior.

