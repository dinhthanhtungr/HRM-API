# Đọc TDS bằng Gemini và xuất Excel

Feature chỉ đọc file và danh mục NVL hiện có; không tạo NVL, import attachment,
gọi `SaveChanges`, lưu kết quả AI vào DB hoặc thêm schema/migration.
Tài liệu được gửi tới Gemini khi người có quyền chủ động tạo job. File nguồn luôn giữ nguyên.

## Quyền và phạm vi

Tất cả endpoint yêu cầu đăng nhập và capability `plm.material-document.extract`.
Fallback tập trung trong `ApplicationPermissionRoleSets` cho Admin, Developer và President;
SaleUser, ACCUser và các role đọc thông thường không tự có quyền này.
Với token có permission set, phải cấp capability trong cấu hình role hiện có;
role Admin cũng không bypass permission set rỗng. Baseline seed/rollback hiện có
được bổ sung claim, nhưng feature không tự chạy SQL hoặc thay đổi DB.
Sau khi quản trị viên cấp claim, cần login/refresh token để nhận quyền mới.

Job, tiến độ và file Excel chỉ cho phép truy cập khi **cả company và người tạo** khớp current user.
Sai scope/owner hoặc job không tồn tại trả 404; thiếu capability trả 403.
Query đối chiếu NVL chỉ lấy material active trong company đã chụp lúc tạo job.
Không gửi danh mục NVL của DB sang Gemini; đối chiếu thực hiện ở backend.

Upload được xử lý như tài liệu do người gọi cung cấp. Với `jobs/source/all`,
nếu server bật `MaterialTdsExtraction:SharedSourceExport` và bỏ trống `SourceCompanyId`,
các thư mục nguồn được dùng chung cho mọi tài khoản có quyền trích xuất. API và worker
đọc toàn bộ nguồn mà không tra danh mục công ty; đối chiếu NVL và tải kết quả vẫn theo
phạm vi đăng nhập. Cấu hình Development đã bật chế độ này. Trong các trường hợp khác,
`MaterialTdsExtraction:SourceCompanyId` nếu đã đặt phải khớp công ty hiện tại. Khi bỏ trống,
API và worker tự dùng công ty duy nhất trong danh mục công ty. Nếu danh mục rỗng, có nhiều
công ty hoặc công ty duy nhất khác công ty đăng nhập, hệ thống từ chối đọc nguồn để tránh
đọc tài liệu từ thư mục của công ty khác. Thư mục cấu hình phải chứa tài liệu
được phép sử dụng bởi công ty đó; không trộn tài liệu của nhiều công ty trong cùng nguồn.
API không nhận đường dẫn tuyệt đối từ client, không trả storage path.

## API

### Tạo job từ thư mục cấu hình

```http
POST /api/v1/plm/materials/tds-extraction/jobs/source?offset=0&limit=30
```

Dùng `MaterialDocumentImport:SourceRoots` (hoặc `SourceRoot`) và scanner hiện có.
Đọc metadata, sắp xếp theo relative path rồi xử lý batch tại `offset`, tối đa `limit` file;
`limit` mặc định bằng `MaxFiles` và không được vượt giới hạn đó.
Sau scan, status trả `sourceOffset`, `sourceTotalFiles`, `nextSourceOffset`;
batch tiếp theo dùng `nextSourceOffset`, null nghĩa đã tới cuối danh sách scan.
Upload job có ba field này null. Giữ thư mục ổn định giữa các batch vì danh sách
không được snapshot xuyên job. Scan bị cắt do `MaterialDocumentImport:MaxFiles` thì
job thất bại `source_scan_truncated`; không xử lý âm thầm một phần scan.
Batch vượt dung lượng trả lỗi; chọn `limit` nhỏ hơn để chạy lại.

### Tạo một workbook từ toàn bộ thư mục

Để xuất chung toàn bộ thư mục, gọi `POST /api/v1/plm/materials/tds-extraction/jobs/source/all`
không cần body hoặc offset. Job quét danh sách một lần, đọc tuần tự qua các batch theo giới hạn
số file và dung lượng, kiểm tra SHA-256 xuyên batch và trả một workbook qua endpoint tải Excel
bên dưới. Tiến độ tính trên toàn danh sách. Giữ nguồn ổn định trong lúc đọc.
Documents ghi nhận cả file lỗi, bản trùng và file phân loại ngoài TDS/SDS/MSDS (`excluded`).
Các file excluded không đóng góp dòng thông số. Cần kiểm tra Documents và Review để phát hiện
phân loại nhầm hoặc tài liệu cần đọc lại. Quyền, giới hạn scan, thời hạn tải và owner giống job source.
Job tổng hợp vẫn giữ kết quả trong RAM đến khi render workbook; dung lượng phụ thuộc số dòng trích.

### Upload nhiều file

```http
POST /api/v1/plm/materials/tds-extraction/jobs/upload
Content-Type: multipart/form-data
```

Gửi nhiều phần cùng tên `files`. Không gửi companyId, đường dẫn nguồn hoặc cấu hình Gemini.
Upload kiểm tra filename, extension, số file, dung lượng khai báo và số byte đọc thực tế.
MIME từ client không được tin; decoder kiểm tra nội dung ảnh/Office, PDF kiểm tra signature
và được Gemini xử lý native. File PDF hỏng/encrypted có thể bị từ chối ở provider.

Cả hai endpoint trả 202 với `Location` tới endpoint tiến độ:

```json
{
  "jobId": "00000000-0000-0000-0000-000000000001",
  "status": "queued",
  "totalFiles": 2,
  "processedFiles": 0,
  "errorCode": null,
  "createdAt": "2026-10-07T03:00:00Z",
  "completedAt": null
}
```

`totalFiles` của source job là 0 trước khi scan; sau đó là số file trong batch.
`processedFiles` gồm file đọc thành công, trùng và lỗi.
`errorCode = null` chỉ có nghĩa không có lỗi cấp job, không bảo đảm mọi file thành công.
`createdAt`/`completedAt` là thời điểm UTC của job tạm, không phải ngày phát hành TDS.
Chỉ có một job queued/running cho mỗi company (409 `company_job_active`).
Queue/result đầy trả 429 `job_capacity_reached`; feature tắt trả 503 `extraction_disabled`.
Sau khi tải Excel, có thể gọi `DELETE /api/v1/plm/materials/tds-extraction/jobs/{jobId}`
để giải phóng job/result tạm và tiếp tục batch mới. Chỉ người tạo trong đúng company
được xóa, job queued/running trả 409; không xóa file nguồn hay dữ liệu DB.

### Tiến độ và tải kết quả

```http
GET /api/v1/plm/materials/tds-extraction/jobs/{jobId}
GET /api/v1/plm/materials/tds-extraction/jobs/{jobId}/excel
```

Status: `queued`, `running`, `completed`, `failed`.
Excel chưa có trả 409 kèm status/errorCode. Khi completed trả file `.xlsx` với `Cache-Control: no-store`.
Lỗi từng file nằm trong Excel và không chặn file còn lại; lỗi scan/query/render cấp job trả `failed`.
Job completed vẫn có thể chứa toàn bộ file lỗi; kiểm tra sheet Documents và Review.

## Chuẩn hóa và trích thông tin

| Định dạng | Cách xử lý | Giới hạn |
| --- | --- | --- |
| PDF | Gửi native PDF, có thể đọc scan bằng thị giác Gemini | Không cam kết đúng OCR; giữ dẫn trang để đối chiếu |
| PNG/JPG/JPEG/JFIF/WEBP/BMP/GIF | Decode, kiểm tra pixel, chuẩn hóa PNG | Tối đa 40 triệu pixel, ảnh nhiều frame phải tách trước |
| DOCX | Lấy đoạn văn và văn bản trong bảng, gắn chỉ số paragraph | Chưa đọc ảnh nhúng, header/footer; không có số trang |
| XLSX | Lấy cached value, áp number format trên ô tạm không có công thức, kèm sheet/cell | Không tính lại công thức, đọc chart/ảnh nhúng hoặc external links; cached value thiếu được đánh dấu |
| DOC/XLS | Chuyển bản sao thành PDF bằng LibreOffice cấu hình | Cần công cụ trên server; timeout 60 giây |
| TXT/CSV | Đọc UTF-8, giữ nguyên nội dung | Encoding khác cần chuyển sang UTF-8 trước |

Office XML/ZIP có giới hạn số entry và tổng dung lượng giải nén; không extract path từ archive.
DOCX/XLSX chứa VBA bị từ chối. Converter legacy dùng profile tạm riêng, tắt macro,
tắt cập nhật link Calc, không dùng profile Office của người dùng. Công cụ chuyển đổi
nên chạy bằng tài khoản worker có quyền tối thiểu; không cài hoặc cấu hình tự động.

Gemini dùng cấu hình `Gemini` hiện có, API key trong header, JSON Schema và system instruction
coi nội dung tài liệu là dữ liệu không đáng tin. Không bật tool, không tìm web và không cho AI thao tác DB.
Response bị block/truncated không được nhận như bản trích đầy đủ. Request tạm thời lỗi
429/5xx/network được retry tối đa 3 lần với delay có giới hạn; dùng chung quota Gemini với CRM.
Các lỗi trả code cố định, không trả/log response provider, secret hoặc nội dung tài liệu.
HTTP 400 trả `gemini_invalid_request`, 401 trả `gemini_authentication_failed`,
403 trả `gemini_permission_denied`, 404 trả `gemini_model_not_found` và 429 trả `gemini_rate_limited`.
Schema gửi Gemini không đặt `maxItems` vì bounds lớn có thể khiến provider từ chối request;
backend vẫn kiểm tra tối đa 30 material codes và 300 chỉ tiêu trước khi nhận kết quả.

## Kiểm tra và ghép NVL

- SHA-256 phát hiện trùng **trong cùng batch**, trước bước chuẩn hóa/gọi AI.
  Chỉ file đã trích thành công mới được dùng làm nguồn skip; không có cache xuyên job.
- Giữ nguyên giá trị, khoảng, dấu bất đẳng thức, đơn vị và điều kiện đo.
  Không lấy trung bình, tự đổi đơn vị hoặc điền số 0 cho giá trị thiếu.
- Kiểm tra tên/giá trị thiếu, evidence/source thiếu, evidence không có trong text đã chuẩn hóa,
  khoảng số đảo ngược, số có dấu phẩy cần đối chiếu định dạng và giá trị số thiếu đơn vị.
  Đây là kiểm tra cấu trúc/các lỗi rõ ràng, không chứng nhận thông số kỹ thuật đúng.
- Một mã được phát hiện khớp duy nhất một NVL active trong company: `exact_match`.
  Mã trong filename và nội dung mâu thuẫn, nhiều mã hoặc nhiều NVL cùng mã: `ambiguous`.
  Tên trùng chỉ tạo `suggested`, không gán `materialId`. Không có ứng viên: `unmatched`.
- Mọi kết quả AI đều yêu cầu kiểm tra; `exact_match` không có nghĩa đã duyệt hoặc đã lưu.

## Excel

- **Documents**: mỗi file một dòng, hash, status `extracted`/`duplicate`/`failed`, phương pháp chuẩn hóa,
  thông tin tài liệu, trạng thái ghép, material ID/code và mã ứng viên.
  Duplicate dẫn tới tên file đã đọc; dòng failed không chứa thông tin AI chưa được kiểm tra.
- **Specifications**: mỗi chỉ tiêu một dòng, giữ giá trị/range dạng text, unit/method/conditions,
  source location, evidence và validation codes. Không có chỉ tiêu thì không tạo dòng giả.
- **Review**: các code cần đối chiếu, lỗi từng file, duplicate và giới hạn Office text-only.
- **Đối chiếu tên**: mỗi cách viết tên chỉ tiêu trong TDS/SDS/MSDS có một dòng, phân biệt dấu,
  hoa/thường và khoảng trắng. Có số tài liệu, số lần xuất hiện và tối đa ba ví dụ kèm tệp.
  QC tự điền tên chuẩn, trạng thái và ghi chú trong Excel. Specifications giữ các dòng gốc cùng SHA-256.

Ô trống nghĩa tài liệu không có giá trị hoặc chưa trích được; `0` nguyên văn vẫn là `0`.
Issue date là chuỗi từ tài liệu, không tự suy ra format ngày. Evidence/page là do AI trích;
với PDF/ảnh chưa có kiểm chứng evidence bằng text deterministic. Mọi ô đều được ghi dạng text,
không biến dữ liệu bắt đầu bằng `=`, `+`, `-`, `@` thành công thức.
Ô dài hơn giới hạn Excel được cắt với dấu `[truncated for Excel]`.
Riêng tên hoặc giá trị chỉ tiêu vượt giới hạn 32.767 ký tự làm file đó báo lỗi
`excel_original_cell_limit` để tránh đưa dữ liệu gốc bị cắt vào bảng đối chiếu.

## Cấu hình vận hành

Feature mặc định tắt. Cấu hình không chứa secret:
`appsettings.Development.json` bật `MaterialTdsExtraction:Enabled` và `Gemini:Enabled`
để thử upload trên Swagger local; API key vẫn phải được cung cấp từ cấu hình hiện có/secret store.

```json
{
  "MaterialTdsExtraction": {
    "Enabled": true,
    "MaxFiles": 30,
    "MaxFileBytes": 10485760,
    "MaxBatchBytes": 41943040,
    "RetentionMinutes": 60,
    "MaxRetainedJobs": 8,
    "MaxTextCharacters": 200000
  }
}
```

Với nguồn riêng và nhiều công ty, đặt `MaterialTdsExtraction__SourceCompanyId` bằng GUID công ty sở hữu thư mục nguồn.
Với một công ty, có thể bỏ cấu hình này.
Đặt `MaterialTdsExtraction__LibreOfficePath` bằng absolute path tới executable nếu cần DOC/XLS.
Đặt `Gemini__Enabled=true`, `Gemini__ApiKey` qua secret store/environment và
`Gemini__Model` bằng model hỗ trợ PDF/ảnh/structured outputs. Không ghi API key vào repo.
Client TDS chỉ gửi key tới HTTPS `generativelanguage.googleapis.com`; không dùng proxy BaseUrl khác host.

Giới hạn cấu hình: 1–100 file, tối đa 10 MiB/file và 40 MiB/batch, 1–8 retained jobs,
text 100–500.000 ký tự, retention 1–1.440 phút. HTTP upload có hard limit 45 MiB gồm multipart overhead.

Job/upload/Excel giữ trong bộ nhớ, không có DB lưu job. Restart API mất job/result;
kết quả hoàn tất hết hạn sau retention và được dọn khi có request mới.
Tải Excel trước khi hết hạn. Không dùng nhiều instance để chia queue; cần sticky routing
hoặc chạy một instance cho thử nghiệm này. Tắt `Enabled` để ngừng nhận job mới;
job đã nhận vẫn xử lý và không có dữ liệu DB cần rollback.

## Verification

Targeted tests kiểm tra auth/explicit permissions, company + owner, batch/file limits,
pipeline upload chỉ đọc DB, SHA-256 skip trước gọi AI, matching mâu thuẫn,
đọc DOCX/XLSX/ảnh, lỗi converter thiếu cấu hình, kiểm tra evidence/range,
Excel giữ literal text và transport Gemini qua fake HTTP.
Test giả lập không đánh giá độ chính xác của model trên TDS thực hoặc xác nhận LibreOffice đã cài.
Pilot thật cần tài liệu đại diện và cấu hình Gemini ở môi trường chạy.
