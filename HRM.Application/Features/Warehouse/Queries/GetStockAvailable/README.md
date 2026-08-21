# Helper tồn kho

`WarehouseStockQueryHelper` gom các rule lọc tồn kho dùng chung cho các màn hình đọc tồn kho:

- lọc theo `CompanyId`;
- bỏ dòng không có `Code`;
- chỉ lấy kệ active (`WarehouseShelves.IsActive == true`);
- giữ constant kệ cân trộn `CT.0.1` và nhãn hiển thị của kệ này.

`GetStockAvailableQueryHandler` dùng helper này nhưng vẫn bao gồm kệ cân trộn trong tổng/detail theo contract tồn kho Warehouse hiện tại.

`keyword` còn tìm theo mã TP Sample Request active và mã VU Formula active cùng company: backend resolve
mã màu Product tương ứng rồi lọc tồn kho theo mã đó.

Material preview cũng dùng helper này, nhưng truyền `excludeMixingShelf: true` để riêng popup NVL không cộng tồn ở kệ cân trộn. Nếu sau này đổi rule kệ active, mã kệ cân trộn hoặc điều kiện nền của tồn kho user-facing, cập nhật helper này trước để các màn hình không bị lệch.
