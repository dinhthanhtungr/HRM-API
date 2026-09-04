# Attachments

## Summary

Attachment flow is split into two responsibilities:

- List and summary APIs return lightweight metadata only: `attachmentId`, `fileName`, `sizeBytes`, `url`, `downloadUrl`, and `isImage`.
- File content is streamed by a dedicated endpoint when the frontend needs to preview or download the file.

Do not return file bytes or base64 content from list, summary, or dashboard APIs. It makes paging heavy and prevents the browser from caching images normally.

## Clean Architecture Layout

Application owns the attachment use case:

- `Features/Attachments/Services/IAttachmentService.cs`
- `Features/Attachments/Services/AttachmentService.cs`
- `Features/Attachments/Dtos/AttachmentDto.cs`

Application also owns the storage abstraction:

- `Abstractions/FileStorage/IFileStorage.cs`

Infrastructure owns the file-share implementation:

- `HRM.Infrastructure/Services/FileStorage/FileShareStorage.cs`
- `HRM.Infrastructure/Services/FileStorage/StorageOptions.cs`

API owns HTTP concerns:

- `HRM.Api/Controllers/Attachments/CollectionAttachmentsController.cs`
- `HRM.Api/Controllers/Attachments/AttachmentsController.cs`
- `HRM.Api/Controllers/PLM/SampleRequestAttachmentsController.cs`

## Storage Rule

The database stores only the relative path in `AttachmentModel.StoragePath`.

Example:

```text
collections/collection-id/Photo/20260623090000000_file.jpg
```

The physical root folder is configured in `HRM.Api/appsettings.json`:

```json
"Storage": {
  "RootPath": "F:\\VIETAUS\\ASP-NET-CORE\\HRM.api\\storage",
  "PublicBaseUrl": null
}
```

New collection attachments are stored below `collections/`. Internal Mail attachments use
`collections/internal-mail/`. Keep old data compatible by pointing `Storage:RootPath` to the
old file root: existing relative paths already stored in the database (including legacy
`attachments/...` paths) continue to be read from their recorded location.

## API Routes

### Business Wrapper: Sample Requests

Use these routes in the Sample Request UI. The API resolves `AttachmentCollectionId`
from `sampleRequestId`, checks ownership on delete, then reuses the shared attachment service.

Upload sample request files:

```http
POST /api/v1/plm/sample-requests/{sampleRequestId}/attachments
POST /api/v1/plm/sample-requests/{sampleRequestId}/attachments?slot=Photo
Content-Type: multipart/form-data
```

List sample request files:

```http
GET /api/v1/plm/sample-requests/{sampleRequestId}/attachments
GET /api/v1/plm/sample-requests/{sampleRequestId}/attachments?slot=SampleRequest
```

Soft delete a sample request attachment:

```http
DELETE /api/v1/plm/sample-requests/{sampleRequestId}/attachments/{attachmentId}
```

### Generic Collection API

Upload one or more files to a collection:

```http
POST /api/collections/{collectionId}/attachmentSchemas?slot=Photo
Content-Type: multipart/form-data
```

`collectionId` must already exist in `Attachment.AttachmentCollection`. For sample requests, this is the value stored in `SampleRequests.SampleRequests.AttachmentCollectionId`.

List files in a collection:

```http
GET /api/collections/{collectionId}/attachmentSchemas
GET /api/collections/{collectionId}/attachmentSchemas?slot=Photo
```

View file content using the old route:

```http
GET /api/collections/{collectionId}/attachmentSchemas/{attachmentId}/content
```

View file content using the short route used by summary DTOs:

```http
GET /api/v1/attachments/{attachmentId}
```

Download instead of inline preview:

```http
GET /api/v1/attachments/{attachmentId}?mode=download
```

Soft delete:

```http
PATCH /api/collections/{collectionId}/attachmentSchemas/{attachmentId}
```

Hard delete metadata and physical file:

```http
DELETE /api/collections/{collectionId}/attachmentSchemas/{attachmentId}/hard
```

## Frontend Usage

For images, use the `url` field directly:

```html
<img src="/api/v1/attachments/{attachmentId}" alt="" />
```

Use `isImage` before rendering an image preview. Non-image files should be rendered as download/open links.

## Notes For Future Changes

- Keep controllers thin. Do not put storage, validation, or EF logic in controllers.
- Keep physical path resolution in `FileShareStorage`.
- Keep slot validation in `AttachmentService` through `AttachmentRules`.
- Use `AttachmentSlot.InternalMail` for files attached to an Internal Mail message; use business slots such as `SampleRequest` only for files attached directly to that business record.
- Add thumbnails as a separate use case or endpoint if large images become slow.
- Do not expose `StoragePath` to clients.
