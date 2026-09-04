# Material document import

## Purpose

The feature scans one backend-configured folder, matches material codes in file
names to active `Material.ExternalId` values in the current company, and can run
an explicit background job to import exact matches. It never deletes or moves
source files.

The preview endpoint only reads metadata; it does not copy, hash, or persist
source files.

The endpoint is limited to the existing administrator role set:

```http
GET /api/v1/plm/materials/document-import/preview
```

The response uses stable codes:

- `sourceStatus`: `available`, `unavailable`, or `not_configured`.
- `slot`: `MaterialTds`, `MaterialMsds`, `MaterialCoa`,
  `MaterialCertificate`, or `MaterialOther`.
- `matchStatus`: `exact_match`, `ambiguous`, or `unmatched`.
- `notes`: machine-readable reasons such as `tds_typo_detected`,
  `material_code_not_detected`, or `material_not_found_in_current_company`.

Only an exact, unique material-code match is returned with `materialId`.
Anything inferred from the common `TSD` typo is marked for review. A file type
that is not recognized defaults to `MaterialOther`; files without a material
code are never matched by material name automatically.

## Configuration

Use a UNC path in production because a Windows service normally cannot see a
drive mapped only in an interactive user session.

One job can scan multiple configured source folders. `SourceRoots` takes precedence over the legacy single `SourceRoot`; when only `SourceRoot` is configured, the existing single-folder behavior remains unchanged.

```json
{
  "MaterialDocumentImport": {
    "SourceRoots": [
      "\\\\file-server\\DATA_HCM\\...\\FILE TIENG ANH",
      "\\\\file-server\\DATA_HCM\\...\\FILE TIENG VIET\\TDS,MSDS",
      "\\\\file-server\\DATA_HCM\\...\\FILE TIENG VIET\\TDS,MSDS hang GRS"
    ],
    "SourceLabel": "TDS/MSDS NVL",
    "IncludeSubdirectories": true,
    "MaxFiles": 10000,
    "AllowedExtensions": [
      ".pdf", ".jfif", ".jpg", ".jpeg", ".png", ".gif", ".webp",
      ".bmp", ".txt", ".csv", ".doc", ".docx", ".xls", ".xlsx",
      ".odt", ".ods"
    ]
  }
}
```

The equivalent environment variables for the source paths are:

```text
MaterialDocumentImport__SourceRoots__0=\\file-server\DATA_HCM\...
MaterialDocumentImport__SourceRoots__1=\\file-server\DATA_HCM\...
MaterialDocumentImport__SourceRoots__2=\\file-server\DATA_HCM\...
```

The OS account running the API must have read permission on every configured share. The API
never accepts a source path from the client and never returns the absolute configured path. When multiple roots are configured, the API prefixes its internal relative file path with a source index so a queued job always reopens the file under the same configured root; this internal path is never a client-supplied filesystem path.

## Matching rules

Examples:

| Filename fragment | Slot | Material code |
| --- | --- | --- |
| `F12 TDS NVL_NH_430` | `MaterialTds` | `NVL_NH_430` |
| `F13 MSDS NVL_BM_642` | `MaterialMsds` | `NVL_BM_642` |
| `COA NVL_NH_430` | `MaterialCoa` | `NVL_NH_430` |
| `CERTIFICATE NVL_NH_430` | `MaterialCertificate` | `NVL_NH_430` |
| `TSD_NVL_PG_353` | `MaterialTds`, review | `NVL_PG_353` |
| `CATALOGUE NVL_NH_430` | `MaterialOther` | `NVL_NH_430` |

Matching is company-scoped and case-insensitive after normalizing spaces,
hyphens, and underscores. Preview remains read-only; the automatic job below is
the only endpoint in this feature that copies files into managed storage.

## Automatic import job

Administrators can enqueue one automatic import job per company:

```http
POST /api/v1/plm/materials/document-import/jobs
```

The endpoint returns `202 Accepted` with a `jobId`. If that company already has
a queued or running job, it returns `409 Conflict` with the active job instead.

Poll progress and load the exception list with:

```http
GET /api/v1/plm/materials/document-import/jobs/{jobId}
GET /api/v1/plm/materials/document-import/jobs/{jobId}/exceptions
```

The worker processes files sequentially. A file is imported automatically only
when its filename contains exactly one normalized material code and that code
matches exactly one active material in `CurrentUser.CompanyId`. Unknown document
kinds use `MaterialOther`. Files without a code, with multiple codes, with no
company-scoped match, or with duplicate material codes are sent to `exceptions`.

For each importable file the worker:

1. Reopens the relative source path under the configured root and blocks path
   traversal or extensions outside the allow-list.
2. Calculates SHA-256 and skips a hash already active in the material's
   attachment collection.
3. Creates and assigns an `AttachmentCollection` if the material has none.
4. Copies the file into managed attachment storage through `IAttachmentService`,
   stores `ContentHash`, and keeps the source file unchanged.

Job status and exceptions are intentionally held in application memory so this
phase needs no database migration. Restarting the API clears job history and an
in-progress job. Imported attachments remain durable; starting another job is
safe because new imported files have a SHA-256 hash and are skipped as duplicates.

If `source_scan_truncated` is returned, increase `MaterialDocumentImport:MaxFiles`
and start a new job. The worker does not import a partial truncated scan because
that could repeatedly process only the first part of a large folder.
