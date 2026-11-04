# Excel Special

`GET /api/v1/excel-special/merchandise-orders/va/excel` tải file
`merchandise-orders-va.xlsx`, sheet `Merchandise Order VA`, gồm ba cột theo thứ tự
`Merchandise Order ExternalId`, `ColourCode`, `VA ExternalId`.

Query tùy chọn:

- `from`, `to`: lọc **CreateDate của MerchandiseOrder**, bao gồm trọn hai ngày đầu/cuối.
  Có thể gửi riêng một đầu; khoảng đảo ngược hoặc ngày kết thúc 9999-12-31 bị từ chối.
- `merchandiseOrderIds`: chọn đơn bằng GUID, gửi lặp query key để chọn nhiều đơn.
  Không gửi hoặc mảng rỗng lấy mọi đơn phù hợp bộ lọc; GUID rỗng bị từ chối.

Ví dụ: `?from=2026-10-01&to=2026-10-13`.
Không có query lấy toàn bộ đơn active trong công ty, không phân trang.

Mỗi dòng là một sản phẩm trong đơn (ProductId) và một VA/lô trên phiếu giao hàng.
VA trùng trong cùng sản phẩm được loại trùng không phân biệt hoa thường;
hai phiếu giao hoặc nhiều dòng đơn cùng sản phẩm–VA được gom thành một dòng Excel.
Không gom VA giữa các sản phẩm khác nhau của đơn.
Mã đơn lấy từ ExternalId của MerchandiseOrder. ColourCode lấy từ Product hiện tại
cùng company của dòng đơn, fallback Product.Code rồi ProductExternalIdSnapshot nếu null/mất quan hệ.
VA/lô lấy từ DeliveryOrderDetailLotConsumption active; chỉ khi không có consumption active
mới fallback LotNoList, tách dấu phẩy/chấm phẩy/pipe/xuống dòng/tab và trim.
Giữ mã lot snapshot trên phiếu, kể cả không còn VA active tương ứng; không lấy VA đang chọn của MFG.

Truy vết bằng MerchandiseOrderDetailId trên dòng giao hàng, không gán mọi lot của
phiếu cho mọi đơn. Chỉ đọc detail đơn và dòng giao active, loại dòng attachment,
phiếu inactive hoặc có status Canceled/Cancelled; phiếu và đơn phải cùng công ty người gọi.
Không bắt phiếu Completed vì mã cần xuất là mã đã ghi trên phiếu giao hàng.
Sản phẩm chưa có VA/lô phù hợp vẫn có một dòng với ColourCode và ô VA trống;
đơn không có dòng active có cả ColourCode/VA trống; không có đơn thì file chỉ có header.
Ví dụ nội dung: `DHG260900213 | OT41059 | VA260900325`.

Endpoint và handler dùng quyền đọc phiếu giao hàng hiện có (DeliveryOrderAccessRules),
kiểm tra đăng nhập và CompanyId hợp lệ. Không thêm capability, không trả vật tư/giá/cost.
Không ghi database, storage hoặc thay đổi schema.
