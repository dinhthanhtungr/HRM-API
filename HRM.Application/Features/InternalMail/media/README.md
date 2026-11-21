# Đính kèm ảnh và video trong hội thoại

## Phạm vi

Video là một loại media của attachment hiện có. Chọn lẫn ảnh/video/tài liệu bằng **cùng nút kẹp giấy**, kéo/thả hoặc dán file. FE xem trước video cục bộ, hiển thị hàng gửi trong hội thoại, tiến trình từng file, dừng và gửi lại. Composer thường được giải phóng trong lúc upload để nhập/gửi tin khác. Không thêm nút gửi video riêng.

Upload có thể tiếp tục áp dụng cho `sendInternalMailMessage`, gồm nhóm nghiệp vụ, nhóm riêng và nhánh R&D. Các action nghiệp vụ riêng qua API Sample Request vẫn giữ multipart và audience hiện hành. Các slot ngoài Internal Mail không tự được mở rộng nhận video.

Ảnh/video mở trong cửa sổ chung, có chuyển trước/sau trong nhóm file của tin hoặc sidebar đã tải. Video.js 8 cung cấp phát/dừng, tua, âm lượng, tốc độ và toàn màn hình, chỉ một player phát một lúc. Video chỉ phát có tiếng khi bấm, không tự phát sau một khoảng chờ. Cuộn hết ra ngoài/ẩn tab/mở viewer thì video trong hội thoại dừng; quay lại không tự phát tiếp. Khả năng điều khiển cụ thể còn phụ thuộc trình duyệt/thiết bị.

Trước gửi, video là thumbnail gọn có thời lượng, dung lượng, nút phát và bỏ chọn; không hiện tên file dài. Viewer nền tối ẩn tiêu đề trực quan nhưng giữ tên truy cập. Download là icon trong control bar Video.js, cùng ẩn/hiện theo thanh tua khi chuột/chạm hoạt động. Bản xem trước cục bộ chưa gửi không có download. Thay đổi UI này không đổi quyền hoặc API tải tệp.

**Không bắt buộc FFmpeg để xem video gốc.** Khi chưa có bản chuẩn hóa (Disabled, Processing, Failed hoặc metadata chưa tải được), viewer dùng contentUrl có quyền và MIME gốc. Khi Ready lúc mở, ưu tiên playbackUrl MP4 đã chuẩn hóa. Nguồn được chốt khi mở để worker hoàn tất không làm video đang xem bị khởi động lại. Metadata/ảnh bìa lỗi không chặn phát gốc; không hiện thông báo hạ tầng như một điều kiện cấm xem. Trình duyệt vẫn có thể không giải mã được codec của file gốc; khi đó có thông báo lỗi, thử lại và tải về, còn FFmpeg giúp tạo bản tương thích.

## Phân loại và response

`AttachmentSlot.InternalMail` là nơi sử dụng; `AttachmentMediaKind` là loại hiển thị: Document=0, Image=1, Video=2, Audio=3, trả dưới dạng chuỗi. Giữ `kind` và `isImage` cũ; isImage vẫn chỉ đúng cho ảnh. Filter `kind=Image` của sidebar là nhóm Ảnh/Video, `kind=File` loại ảnh/video. Loại hiển thị suy từ tên file lịch sử không phải bằng chứng nội dung hợp lệ; ffprobe/FFmpeg kiểm tra khả năng giải mã khi xử lý.

```json
{
  "attachmentId": "<id>", "fileName": "clip.mov", "sizeBytes": 123456,
  "contentType": "video/quicktime", "kind": "Video", "mediaKind": "Video", "isImage": false,
  "contentUrl": "/api/v1/internal-mail/attachments/<id>",
  "downloadUrl": "/api/v1/internal-mail/attachments/<id>?mode=download",
  "thumbnailUrl": null,
  "videoInfoUrl": "/api/v1/internal-mail/attachments/<id>/video/info",
  "videoRetryUrl": "/api/v1/internal-mail/attachments/<id>/video/retry",
  "posterUrl": "/api/v1/internal-mail/attachments/<id>/video/poster",
  "playbackUrl": "/api/v1/internal-mail/attachments/<id>/video/playback"
}
```

Các field URL video null với file khác. FE dùng URL BE cung cấp. Tệp liên quan SampleRequest dùng cùng hậu tố dưới route conversation-scoped hiện có. Không trả storage path.

## Gửi và chống trùng

FE giữ File/payload trong bộ nhớ theo phiên đăng nhập và tạo UUID cho lần gửi. Server tạo session thuộc company/employee/conversation, nhận tuần tự các chunk tối đa 1 MiB. Mất response phải đọc offset trước retry. Server chỉ chấp nhận đúng offset, rollback phần ghi nếu chunk lỗi/quá lớn. File phải đủ byte mới được gửi.

Complete khóa session, kiểm tra lại quyền và gọi SendInternalMessageCommand hiện hành. UUID session là ID message. Retry sau commit mất response trả message đã lưu của đúng người/company/conversation, không tạo notification lần hai; R&D kiểm tra thêm quan hệ với conversation gốc. Chỉ xác nhận server mới có nghĩa đã gửi, không đồng nghĩa đã đọc. Sửa payload/chọn bộ file khác là lần gửi mới.

Dừng upload giữ phần đã nhận để tiếp tục. UI không cho hủy trong bước xác nhận commit. Tin có nhiều file chỉ lưu sau khi mọi file tải đủ; xử lý video là trạng thái riêng. FE xóa dữ liệu upload tạm sau khi xác nhận gửi; session còn lại hết hạn sau 24 giờ. F5/đóng tab/đăng xuất không giữ File trong browser storage.

### API

Các route dưới đây tương đối từ `/api/v1/internal-mail`, đều yêu cầu `[Authorize]`, company/employee hợp lệ và quyền gửi hiện hành. Chỉ người tạo session đọc/ghi file tạm của mình.

| Method | Route | Contract |
| --- | --- | --- |
| POST | `/uploads` | `{ sessionId, conversationId, areaCode, files: [{ fileName, contentType, length }] }`; tạo/đối chiếu session |
| GET | `/uploads/{id}` | `{ sessionId, offsets: [0, 1048576] }`; byte đã nhận theo thứ tự file |
| PUT | `/uploads/{id}/files/{index}?offset=...` | Body application/octet-stream, tối đa 1048576 byte; trả `{ offset }` mới |
| POST | `/uploads/{id}/complete` | `{ body, mentionedEmployeeIds, replyToMessageId, isUrgent }`; conversation/area lấy từ session |
| DELETE | `/uploads/{id}` | Xóa dữ liệu upload tạm, không xóa tin đã gửi |

404: session thiếu/hết hạn/ngoài quyền. 400: tên/loại/size/offset không hợp lệ. 413 có thể được trả trước service khi body quá lớn. Complete giữ OperationResult hiện hành; replay trả notificationId=Guid.Empty vì không publish lại. Offset 0 nghĩa chưa tải, không phải lỗi.

Tối đa 10 file, tổng **50 MiB/message**, tối đa 3 session chờ/employee. Chặn path không an toàn, executable/script/active content; video extension phải phù hợp MIME khai báo. MIME client không thay bước kiểm tra giải mã. Giữ route gửi JSON/multipart cũ cho client cũ.

## Xử lý video và quyền phát

Handler đưa video mới vào queue sau bước lưu message; video lịch sử được đưa vào queue qua metadata request đầu tiên. Worker dùng file lock, copy nguồn vào vùng riêng, đọc ffprobe rồi tạo MP4 H.264/AAC faststart và ảnh bìa JPEG. Giới hạn cạnh bản phát 1280, đầu ra 30 fps; giữ bản gốc để tải xuống. Timeout/hủy kill cây process, finally dọn input và output tạm. Có whitelist demuxer/protocol để không tải nguồn mạng; truyền argument riêng, không gọi shell.

`GET videoInfoUrl` trả `{ "status": "Ready", "durationSeconds": 12.3, "width": 1920, "height": 1080 }`. Width/height là kích thước **nguồn**. Duration/kích thước null khi chưa có kết quả.

| Status | Ý nghĩa |
| --- | --- |
| Processing | Đang chờ/xử lý, chưa có bản phát hoàn chỉnh |
| Ready | MP4 và ảnh bìa đã ghi hoàn chỉnh |
| Failed | File hỏng/không tương thích/quá giới hạn, thiếu FFmpeg hoặc xử lý lỗi |
| Disabled | Chưa bật xử lý theo cấu hình môi trường |

`POST videoRetryUrl` chỉ reset Failed, tối đa 3 lần/file. GET/reload không tự reset lỗi. Poster/playback chưa Ready trả 404. Các endpoint đều kiểm tra lại company, active, quyền conversation và visibility message/collection như file gốc. File bị thu hồi quyền/xóa mềm không còn phát qua API. Response private/no-store; playback hỗ trợ HTTP Range để phát/tua không cần tải hết vào Blob.

Player dùng cookie HttpOnly hiện hành và crossorigin=use-credentials. Không đưa JWT vào URL, không mở rộng token-query của SignalR. Phải cấu hình HTTPS/cookie/CORS credentials đúng origin FE, kiểm tra riêng Capacitor/WebView.

## Realtime

Khi Ready, publisher ghi outbox loại `InternalMailMediaReady` vào bảng hiện có. Processor kiểm tra lại attachment/message/company và reader scope trước dispatch. Event `internalMailMediaReady` chỉ chứa `{ attachmentId }`, gửi tới nhóm user hợp lệ để FE đọc lại metadata. Không tạo notification/feed/unread/Web Push, không truyền tên/nội dung/path/token. Quyền tải file vẫn do API quyết định.

FE poll 4 giây khi video đang được nhìn thấy/mở làm đường dự phòng; dừng ở Ready/Failed/Disabled hoặc sau khoảng 10 phút. Queue đông có thể chờ lâu hơn; mở lại để lấy trạng thái mới. Lỗi enqueue signal không đổi Ready thành Failed.

## Cài đặt và release

Cài FFmpeg/ffprobe có libx264 và AAC trên host worker. Tài khoản dịch vụ cần quyền chạy binary và đọc/ghi Storage:RootPath. Vùng storage phải riêng tư, không public static hosting. Không cần wrapper NuGet.

```json
{
  "VideoMedia": {
    "Enabled": true,
    "FfmpegPath": "<absolute-path-to-ffmpeg>",
    "FfprobePath": "<absolute-path-to-ffprobe>",
    "TimeoutSeconds": 300,
    "MaxDurationSeconds": 1800
  }
}
```

Source mặc định **Enabled=false** để không chạy bộ xử lý khi host chưa có FFmpeg. Triển khai BE trước FE, sau đó cấu hình/bật theo quy trình release. Không tự cài vào/restart máy chủ sản xuất bằng thay đổi source này. Rollback FE vẫn dùng được API multipart cũ; không xóa kho file để rollback.

Không thêm bảng/cột/migration. Session nằm dưới `.attachment-uploads/{company}/{employee}/{session}`; job/metadata/bản phát ở `.video-media/{attachment}` dưới Storage:RootPath. Cleaner xóa dữ liệu session hết hạn mỗi 15 phút và khi tạo session mới. Bản phát giữ theo retention storage, không tự xóa file nghiệp vụ. Không có transaction chung DB-filesystem; ID message bảo vệ retry và polling bảo vệ mất signal.

Nhiều API instance cần cùng storage và filesystem hỗ trợ semantics file lock; phải kiểm thử triển khai cụ thể, không mặc định hỗ trợ mọi NFS/object storage. Worker xử lý tuần tự để giới hạn CPU. Theo dõi dung lượng, queue và giới hạn proxy/IIS cho chunk 1 MiB; bước commit cần timeout phù hợp.

## Verification và giới hạn

| Kiểm tra source | Kết quả |
| --- | --- |
| Build HRM.Api, output kiểm tra riêng | Pass, 0 lỗi; còn cảnh báo dependency ImageSharp hiện có |
| Test BE tập trung worker/upload/quyền/replay | 17 pass |
| Test metadata/route và nhóm kiểm tra liên quan | 77 pass ở lượt kiểm tra metadata/upload/quyền |
| Build FE production | Pass |
| Test FE transport/upload/realtime bị ảnh hưởng | 191 pass |
| Test FE media/điều khiển thủ công/thẻ trước gửi | 16 pass; tổng 9 file liên quan: 207 pass |
| Build FE và lint nhóm Media/PendingVideoAttachment | Pass |
| Chrome desktop với video thực | Bấm phát có tiếng; viewer nền tối ẩn tên; icon download trong control bar, opacity cả thanh về 0 khi không thao tác |
| Hồi quy viewer: mở nguồn gốc khi Disabled/Failed/Processing, bản MP4 khi Ready | 4 pass; kiểm tra source/MIME truyền vào player, chưa phải kiểm thử codec thực tế |
| Lint component media/upload và API mới | Pass |
| Chuyển mã/phát trên host và thiết bị thật | Chưa xác nhận; chưa có ffmpeg/ffprobe trong PATH của môi trường kiểm tra |

Test tự động bao phủ resume/restart store, byte/offset, rollback chunk, isolation company/employee, session hết hạn/hủy, tên/MIME/size không an toàn, metadata/route và caller gửi tin. Test FE kiểm tra file hỗn hợp, lost response, session ID khi commit chưa rõ và hồi quy transport. Đây không phải bằng chứng phát video trên thiết bị thật.

Cần UAT MP4/MOV/WebM: nút cũ/kéo-thả/xem trước, gửi lẫn ảnh, chuyển hội thoại, gửi thêm tin trong khi upload, mất mạng/thử lại, phát/tua, đổi ảnh/video, toàn màn hình, cookie hết hạn và thu hồi membership. Kiểm tra desktop, Android/Capacitor và iPhone trong phạm vi sản phẩm.

Chưa có HLS nhiều chất lượng, gửi nền khi app đóng/khóa, phục hồi File sau F5, album xuyên lịch sử chưa tải. Không khẳng định hoàn toàn giống Zalo chỉ từ build/test. Video.js đã cài ở FE; FFmpeg vẫn là dependency vận hành phải có trên host.
