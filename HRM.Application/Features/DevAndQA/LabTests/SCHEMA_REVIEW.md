> LabTest đang tạm dừng; tài liệu bên dưới giữ để khôi phục sau. Xem [trạng thái hiện tại](README.md#trạng-thái-hiện-tại-tạm-dừng).

# Review schema LabTest trước rollout

Chỉ mô tả model cần triển khai. Chưa sinh/chạy migration/SQL hoặc kết nối DB.

Nếu chưa có bảng: cần LabTest và LabTestResult theo EF configurations, shared EvaluationStandard/MeasurementMethod/EvaluationTemplate, Company, Employee và AttachmentCollection hiện có. Nếu đã có LabTest theo lần đồng bộ trước: chỉ bổ sung metadata bên dưới, giữ dữ liệu cũ.

| Bảng | Cột mới | Kiểu/null |
| --- | --- | --- |
| LabTest | suppliercustomername | varchar(300), nullable |
| LabTest | batchnumber/itemcode/resin | varchar(100), nullable |
| LabTest | application | varchar(2000), nullable |
| LabTest | preparedbyname | varchar(200), nullable |
| LabTest | weightkg | numeric(18,6), nullable |
| LabTest | importedphotoname | varchar(255), nullable |
| LabTest | isdeleted | boolean, NOT NULL default false |
| LabTest | importbatchid | uuid, nullable |
| LabTest | importrownumber | integer, nullable |
| LabTestResult | observedtext | text, nullable |

Unique index mới `(companyid, importbatchid, importrownumber)` chống import trùng; nullable trên phiếu tạo thủ công. Giữ unique `(companyid,code)`, `(labtestid,criterioncode)`, `(labtestid,evaluationstandardid)`, các FK Restrict và check snapshot hiện có. Không tự tạo/xóa danh mục hoặc cập nhật Value từ quan sát.

Trước deployment: kiểm tra bảng/cột/index/FK thực tế, backup, chạy schema review/migration được duyệt trên bản sao, giữ isdeleted=false cho phiếu cũ. Nullable metadata không cần suy giá trị/backfill từ catalogue hiện tại. Deploy code sau schema hoàn tất.

Rollback code không xóa bảng/cột/dữ liệu import. Nếu đã có dữ liệu metadata/ObservedText, không drop cột; lập phương án bảo toàn riêng. Kiểm tra song song schema hai repo nếu sau này được cho phép đồng bộ Backend; ngoại lệ lần trước không áp dụng cho rollout này.
