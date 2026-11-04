# Helper tồn kho

`WarehouseStockQueryHelper` gom các rule lọc tồn kho dùng chung cho các màn hình đọc tồn kho:

- lọc theo `CompanyId`;
- bỏ dòng không có `Code`;
- chỉ lấy kệ active (`WarehouseShelves.IsActive == true`);
- giữ constant kệ cân trộn `CT.0.1` và nhãn hiển thị của kệ này.

`GetStockAvailableQueryHandler` có query tổng quan riêng cho phép xem cả kệ đã tắt, cùng company.
`TotalOnHandKg` và `AvailableKg` vẫn chỉ lấy tồn kệ active; các field mới và hướng dẫn FE nằm ở
[contract Warehouse](../../README.md#hiển-thị-tồn-trên-kệ-đã-tắt-fe).
Kệ cân trộn CT.0.1 chỉ xuất hiện khi có capability `warehouse.mixing-stock.view` (mặc định:
PLPUUser, Developer, President). Không có quyền thì loại trước khi phân nhóm, tính tổng và phân trang;
kiểm tra cả ShelfStockCode và WarehouseShelves.SlotCode. Company/customer scope và rule kệ inactive giữ nguyên.

`keyword` còn tìm theo mã TP Sample Request active và mã VU Formula active cùng company: backend resolve
mã màu Product tương ứng rồi lọc tồn kho theo mã đó.

Material preview vẫn dùng helper active, truyền `excludeMixingShelf: true` để riêng popup NVL không cộng tồn ở kệ cân trộn. Không nới helper active để phục vụ hiển thị kệ tắt: các consumer chọn tồn nghiệp vụ phải tiếp tục loại kệ tắt.
