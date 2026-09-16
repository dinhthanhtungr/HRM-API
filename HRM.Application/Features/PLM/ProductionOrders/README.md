# Lệnh sản xuất MFG

Feature cung cấp các thao tác đọc cho `manufacturing.MfgProductionOrders`.

## API kiểm tra Product đang sản xuất

`GET /api/v1/plm/production-orders/products/{productId}/is-in-production`

Endpoint yêu cầu đăng nhập và trả trạng thái kiểm tra cùng danh sách mã
`ManufacturingFormula` hiện hành của các lệnh thỏa điều kiện:

```json
{
  "isInProduction": true,
  "manufacturingFormulaExternalIds": [
    "VA260900001",
    "VA260900002"
  ]
}
```

- `isInProduction = true`: trong công ty hiện tại có ít nhất một
  `MfgProductionOrder` active của Product ở một trong các trạng thái đang sản xuất
  được quy định bên dưới.
- `isInProduction = false`: không có lệnh thỏa điều kiện, `productId` rỗng, hoặc
  current user không có `CompanyId` hợp lệ.
- `manufacturingFormulaExternalIds` lấy từ `ProductionSelectVersion` hiện hành
  (`ValidFrom != null`, `ValidTo == null`) của từng lệnh, lọc cùng công ty, bỏ mã
  rỗng, chống trùng không phân biệt hoa thường và sắp xếp tăng dần.
- Danh sách có thể rỗng dù `isInProduction = true` khi lệnh thỏa trạng thái nhưng
  chưa gắn `ManufacturingFormula` hiện hành.
- Dữ liệu luôn được lọc bằng `CompanyId` của current user để tránh đọc chéo công ty.

## Trạng thái MfgProductionOrder

Các trạng thái được khai báo trong enum `ManufacturingProductOrder`:

1. `Unknown`
2. `New`
3. `FormulaRequested`
4. `FormulaSuccess`
5. `Scheduling`
6. `Scheduled`
7. `Waiting`
8. `Unassign`
9. `Getback`
10. `QCinprogress`
11. `QCPassed`
12. `QCFail`
13. `BTPNew`
14. `Weighting`
15. `Weighted`
16. `Mixing`
17. `Mixed`
18. `Started`
19. `Canceled`
20. `Running`
21. `Finished`
22. `Change`
23. `Unassignfrommd`
24. `Reported`
25. `Done`
26. `Stocked`

Trong contract của endpoint này, các trạng thái sau mang nghĩa **đang sản xuất**:

`Unassign`, `Getback`, `QCinprogress`, `QCPassed`, `QCFail`, `BTPNew`, `Weighting`,
`Weighted`, `Mixing`, `Mixed`, `Started`, `Canceled`, `Running`.

Lưu ý: `Canceled` được tính là đang sản xuất theo contract nghiệp vụ hiện tại của
endpoint này.

Feature không ghi database, không publish notification và không thay đổi
SignalR/Web Push.
