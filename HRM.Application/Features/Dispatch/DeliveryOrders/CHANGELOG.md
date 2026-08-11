# Delivery Orders Changelog

## 2026-08-05

- Phase 4: chuyển API đọc, timeline, complaint source và Executive PnL sang `DeliveryOrderDetailLotConsumption` làm nguồn chuẩn.
- Giữ `LotNoList` làm fallback cho dữ liệu lịch sử chưa backfill.
- Mở rộng backfill admin-only với dry-run và reconciliation quantity theo current company.
- Không xóa cột legacy và không tạo migration.
