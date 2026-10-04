# Delivery Orders Changelog

## 2026-09-23

- Thêm GET `{id}/pdf` và `{id}/excel` cho từng phiếu, company-scoped, dùng quyền đọc hiện có.
  Dùng chung projection chứng từ không có cost; xuất file không ghi nhận đã giao hoặc thay đổi dữ liệu.

- Thống nhất rule lượng phân bổ cho selectable-lines/create/update; phiếu Canceled/Cancelled
  (kể cả khác hoa/thường hoặc khoảng trắng) không chiếm lượng PO để lập phiếu mới.
- Sửa tổng tồn khả dụng nhiều lot: không vượt tồn product sau reserve chưa gắn lot.
- Bổ sung hướng dẫn FE, cấu trúc Clean Architecture và đối chiếu chức năng legacy đã có/chưa chuyển.
- Giữ contract route/DTO, quyền/cost visibility, không thêm migration hay import dữ liệu.

## 2026-08-05

- Phase 4: chuyển API đọc, timeline, complaint source và Executive PnL sang `DeliveryOrderDetailLotConsumption` làm nguồn chuẩn.
- Giữ `LotNoList` làm fallback cho dữ liệu lịch sử chưa backfill.
- Mở rộng backfill admin-only với dry-run và reconciliation quantity theo current company.
- Không xóa cột legacy và không tạo migration.
