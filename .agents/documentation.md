# Quy Tắc Tài Liệu

Đọc file này khi task thêm/sửa feature có nghiệp vụ, API, DTO public, rule, bảo mật, side effect, config hoặc cách vận hành.

## Summary

Mỗi feature mới hoặc feature có nghiệp vụ đáng chú ý nên có summary ngắn ở một trong các nơi:

- XML summary trên command/query/handler chính.
- File `.md` gần feature nếu flow dài hoặc FE cần đọc.
- Comment ngắn trước đoạn code có logic nghiệp vụ/bảo mật khó hiểu.

Summary nên nói feature làm gì, ai dùng, API/request/response chính, dữ liệu chính, quyền truy cập, side effect, rule dễ hiểu sai.

## README

Mỗi feature có nghiệp vụ riêng nên có `README.md` tại thư mục gốc feature, ví dụ:

```text
Features/Notifications/README.md
Features/InternalMail/README.md
Features/CRM/CustomerCare/README.md
```

README mô tả trạng thái đang đúng của feature, không phải nhật ký sửa code.

Cập nhật README khi:

- Thêm/sửa/xóa API, DTO hoặc ý nghĩa field public.
- Thay đổi rule nghiệp vụ, status transition, resolve data/người nhận.
- Thay đổi phân quyền, company scope, ownership, điều kiện nhìn/sửa dữ liệu.
- Thêm entity/bảng/quan hệ quan trọng.
- Thêm side effect như audit, notification, email, SignalR, Web Push, file, background worker.
- Thêm config, dependency ngoài, retry, timeout, scheduler, cache.
- Sửa bug làm hành vi đúng khác README hiện tại.

Không bắt buộc cập nhật README cho format, rename biến private, refactor nội bộ giữ nguyên contract, typo, hoặc test bổ sung cho hành vi đã mô tả đúng.

## Tài Liệu Response Contract

Khi thêm/sửa API, DTO public hoặc khi response có nhiều nguồn dữ liệu và trạng thái dễ hiểu sai,
README gần feature phải giải thích contract theo **ý nghĩa nghiệp vụ**, không chỉ liệt kê tên property.

Tối thiểu phải ghi:

- Endpoint và một response mẫu rút gọn nhưng đại diện được các nhánh quan trọng.
- Ý nghĩa từng nhóm field mà FE cần dùng; field trùng tên ở các cấp khác nhau phải nói rõ field nào là canonical.
- Nguồn của dữ liệu: cột DB, snapshot đã lưu, dữ liệu realtime, kết quả tính toán, fallback hay dữ liệu bị che theo role.
- Semantics của `null`, `0`, chuỗi rỗng, danh sách rỗng và các cờ boolean/status liên quan.
- Cách phân biệt dữ liệu đã persist với dữ liệu hệ thống chỉ preview/tính tạm; nêu field id/status/flag dùng để nhận biết.
- Ý nghĩa của các mốc thời gian: ngày của entity nguồn, ngày tính, ngày lưu, ngày duyệt hay ngày phát sinh nghiệp vụ.
- Công thức tính, thứ tự fallback và rule làm tròn nếu response có giá, cost, margin, phần trăm hoặc số liệu tổng hợp.
- Visibility theo role đối với field nhạy cảm; field bị `null`, bị omit hay bị thay bằng DTO rút gọn phải ghi nhất quán.
- Với collection lồng nhau, giải thích mỗi phần tử đại diện cho gì và cờ như `isStored`, `isCalculated`, `isComplete`
  ảnh hưởng cách FE hiển thị/lưu ra sao.

Không coi một JSON dump hoặc danh sách tên property là tài liệu đủ. Sau khi đổi mapper/calculator/visibility,
agent phải đối chiếu README với code đang chạy để tránh mô tả contract cũ.

## CHANGELOG

Nếu cần lịch sử thay đổi, dùng `CHANGELOG.md` gần feature. Mỗi entry ngắn, có ngày `YYYY-MM-DD`, loại thay đổi và tác động.

Không ghi changelog cho format/refactor không ảnh hưởng user, API, dữ liệu, bảo mật hoặc vận hành.

## Encoding

Với file source C# và `.md`, ưu tiên tiếng Việt có dấu. Sau khi thêm summary/comment tiếng Việt có dấu, đọc lại để xác nhận UTF-8 không bị mojibake.

