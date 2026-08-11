# File Upload, Attachment, Storage

Đọc file này khi task đụng attachment, upload/download, storage, file share, public URL hoặc file metadata.

## Upload

- Check file size.
- Check extension/MIME.
- Không lưu file theo raw filename của user nếu có rủi ro path traversal.
- Normalize/sanitize filename nếu cần hiển thị.

## Authorization

- Tải/xem/xóa file phải check quyền với entity cha.
- Attachment user-facing phải lọc theo company scope/ownership của entity cha.
- Không trả storage path nội bộ nếu FE chỉ cần URL/id.

## Storage Config

- Root path, public base URL, retry, timeout phải dùng config/options nếu có khả năng đổi theo môi trường.
- Không hard-code UNC/local path thật trong code.
- Không ghi secret storage vào source.

