# Delivery Orders Changelog

## 2026-10-15 — Tìm tất cả đơn trong kế hoạch giao hàng

- GET delivery-plan thêm includeAllOrders=true để bỏ giới hạn ngày và gồm đơn chưa có ngày hứa; mặc định false giữ luồng cũ. Giữ scope, trạng thái nguồn, keyword và phân trang.
- Response thêm includeAllOrders; promisedDeliveryDate có thể null trong chế độ tất cả đơn, isOverdue=false khi chưa có ngày hứa.

## 2026-10-14 — Đồng bộ DHG khi lưu

- POST/PUT/PATCH đồng bộ RealQuantity và trạng thái dòng/header DHG trong transaction, bao gồm phiếu Pending chưa in; Cancel/DELETE tính lại từ phiếu còn hiệu lực.
- POST print chỉ ghi HasPrinted và xuất PDF, không cộng lượng. Không đổi schema; phiếu cũ cùng DHG được tính khi lưu/hủy/xóa, không backfill toàn DB.

## 2026-10-13

- Lookup và tạo/sửa PGH dùng tồn thực tế theo product/lot/stockType, không trừ reserve. reservedQuantity giữ để tham khảo; không sửa dữ liệu giữ chỗ kho. Vẫn chặn giao vượt tồn lot và tổng product/type.

## 2026-10-12

- PDF phục hồi bố cục legacy VA-WH-F18(05), đóng gói logo/chứng nhận và hiển thị nhiều số VA trong cùng dòng. Nhãn ngày và tổng cộng PDF theo legacy; Excel giữ contract hiện tại.
- Thêm số bao từng lot POST/PUT và lotBags PATCH để sync PRQ Pending; detail trả số bao phân bổ đã lưu. Lookup/detail trả bagWeightKg từ yêu cầu mẫu cùng product/khách/company để FE gợi ý.

## 2026-10-10

- Cho phép giao Hàng lỗi; kiểm tra riêng product/lot/stockType. PGH giữ snapshot lot tổng, PRQ lưu ItemStockType từng lot; GET detail trả stockAllocations. Không thêm migration hoặc ghi tồn kho.

## 2026-10-09

- Selectable-lines loại PO KH_VIETAUS theo mã snapshot hoặc khách hiện tại trước đếm và phân trang.

## 2026-10-07

- Cờ trễ hẹn so ngày hứa giao với hôm nay UTC+7, không với ngày kế hoạch đang xem;
  ngày hứa hôm nay chưa trễ, đơn đã đủ lượng không bị gắn trễ hẹn.

## 2026-10-06

- Thêm lines[].importedQuantity trong kế hoạch, dùng chung lịch sử nhập kho với timeline,
  giữ quy thuộc lot và chống cộng trùng MFG; không đổi lượng giao hoặc tác động tồn kho.

## 2026-10-05

- Thêm GET delivery-plan/excel nhận from/to, dùng chung bộ xuất Excel Plan theo mẫu legacy.
- Loại mọi PO KH_VIETAUS khỏi kế hoạch theo mã khách snapshot hoặc mã hiện tại.
- Thêm status PO cho kế hoạch, đồng nhất trạng thái đơn hàng và rule Paused hiện tại;
  giữ progress riêng cho tiến độ theo lượng để FE không dùng thay trạng thái đơn.
- Kế hoạch loại PO đã chốt Delivered/Completed hoặc hủy Canceled/Cancelled, bất kể lượng phiếu;
  áp dụng trước bộ lọc, đếm và phân trang để đơn đóng không quay lại danh sách quá hạn.
- Thống nhất lượng kế hoạch và timeline từ phiếu active chưa hủy cho mọi PO; timeline loại
  phiếu Canceled/Cancelled khỏi lượng và chi tiết giao. Bổ sung regression giao một phần 123,5/125.
- Fix delivery-plan: công nhận lượng phiếu còn hiệu lực khi PO đã chốt Delivered/Completed,
  tránh PO giao đủ bị báo chưa giao và xuất hiện sai trong phần quá hạn; dùng chung rule cho query và chi tiết.
- Thêm GET delivery-plan: ngày mai UTC+7, gộp PO theo ngày hứa MFG muộn nhất, giữ đơn đúng ngày
  và quá hạn còn thiếu; lượng đã giao chỉ tính phiếu Completed. Thêm DTO, tài liệu và regression tests.
- Chuyển nghiệp vụ legacy theo MediatR hiện tại: PGH/PRQ cùng transaction, hàng kèm, PATCH metadata/số bao,
  POST print idempotent, DELETE rollback lượng đã in/hủy PRQ, finish PO Delivering và EventLog.
- Thêm báo cáo Excel Plan/Finish/Transport cùng mẫu legacy; PDF có nhận diện công thức một thành phần.
- Thêm tạo người giao Admin-only, hasPrinted, trạng thái tạm dừng giao và filter PO ở selectable-lines.
- Giữ GET file read-only, company scope, cost visibility và schema; PUT chỉ Pending chưa in và PRQ chưa xử lý.
- Bổ sung LEGACY-CONTRACT, cập nhật mapping/README/flow và regression test.
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
