using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HRM.Domain.Enums.Attachment;

namespace HRM.Domain.Security.Rules.Attachment
{
    public static class AttachmentRules
    {
        public const long MB = 1024L * 1024L;

        public static readonly IReadOnlyDictionary<AttachmentSlot, SlotRule> Map =
            new Dictionary<AttachmentSlot, SlotRule>
            {
                // HỢP ĐỒNG: thường 1 bản cuối cùng. Ưu tiên PDF.
                // Cho phép kèm Word (doc/docx) nếu quy trình nội bộ còn chỉnh sửa.
                [AttachmentSlot.Contract] = new SlotRule
                {
                    AllowMultiple = false,
                    AllowedMimePrefixes = new[]
                    {
                        "application/pdf",
                        "application/msword",
                        "application/vnd.openxmlformats-officedocument"
                    },
                    MaxBytes = 20 * MB
                },

                // ĐƠN ĐẶT HÀNG (PO): thường 1 file (PDF hệ thống hoặc scan).
                // Cho phép "image/" nếu thực tế hay dùng ảnh scan.
                [AttachmentSlot.PurchaseOrder] = new SlotRule
                {
                    AllowMultiple = true,
                    AllowedMimePrefixes = new[]
                    {
                        "application/pdf",
                        "image/",
                        "text/",
                        "application/zip",
                        "application/x-7z-compressed",
                        "application/x-rar-compressed",
                        "application/msword",
                        "application/vnd.ms-excel",
                        "application/vnd.openxmlformats-officedocument"
                    },
                    MaxBytes = 50 * MB
                },


                // PHIẾU GIAO HÀNG (Delivery Note): 1 bản, phổ biến là PDF.
                [AttachmentSlot.DeliveryNote] = new SlotRule
                {
                    AllowMultiple = false,
                    AllowedMimePrefixes = new[] { "application/pdf" },
                    MaxBytes = 20 * MB
                },

                // HÓA ĐƠN: 1 bản, chuẩn PDF.
                [AttachmentSlot.Invoice] = new SlotRule
                {
                    AllowMultiple = false,
                    AllowedMimePrefixes = new[] { "application/pdf" },
                    MaxBytes = 20 * MB
                },

                // ẢNH: cho nhiều file, bật GenerateThumbnail để tạo thumbnail sau khi upload.
                [AttachmentSlot.Photo] = new SlotRule
                {
                    AllowMultiple = true,
                    AllowedMimePrefixes = new[] { "image/" },
                    MaxBytes = 10 * MB,
                    GenerateThumbnail = true
                },

                // SPEC/tiêu chuẩn kỹ thuật: thường PDF nặng, đôi khi kèm ảnh hoặc bảng tính (xlsx).
                [AttachmentSlot.Specification] = new SlotRule
                {
                    AllowMultiple = true,
                    AllowedMimePrefixes = new[]
                    {
                        "application/pdf",
                        "image/",
                        "application/vnd.ms-excel",
                        "application/vnd.openxmlformats-officedocument"
                    },
                    MaxBytes = 50 * MB
                },

                // KHÁC: cho nhiều, mở rộng các loại an toàn phổ biến.
                // Lưu ý: nếu không muốn nhận file nén/rar/7z vì lí do bảo mật, hãy bỏ bớt.
                [AttachmentSlot.Other] = new SlotRule
                {
                    AllowMultiple = true,
                    AllowedMimePrefixes = new[]
                    {
                        "application/pdf",
                        "image/",
                        "text/",
                        "application/zip",
                        "application/x-7z-compressed",
                        "application/x-rar-compressed",
                        "application/msword",
                        "application/vnd.ms-excel",
                        "application/vnd.openxmlformats-officedocument"
                    },
                    MaxBytes = 50 * MB
                },


                // Yêu cầu mẫu.
                // Cho phép "image/" nếu thực tế hay dùng ảnh scan.
                [AttachmentSlot.SampleRequest] = new SlotRule
                {
                    AllowMultiple = true,
                    AllowedMimePrefixes = new[]
                    {
                        "application/pdf",
                        "image/",
                        "application/msword",
                        "application/vnd.openxmlformats-officedocument",
                    },
                    MaxBytes = 20 * MB
                },

                [AttachmentSlot.ColouredChip] = new SlotRule
                {
                    AllowMultiple = true,
                    AllowedMimePrefixes = new[]
                    {
                        "image/",            // ảnh (jpg/png/webp…)
                        "application/pdf"    // report/scan
                    },
                    MaxBytes = 20 * MB,      // mỗi file tối đa 20MB
                    GenerateThumbnail = true // có ảnh thì tạo thumbnail
                },

                [AttachmentSlot.QcReport] = new SlotRule
                {
                    AllowMultiple = true,
                    AllowedMimePrefixes = new[]
                    {
                        "image/",            // ảnh (jpg/png/webp…)
                        "application/pdf"    // report/scan
                    },
                    MaxBytes = 10 * MB,      // mỗi file tối đa 10MB (tuỳ bạn)
                    GenerateThumbnail = true // có ảnh thì tạo thumbnail
                },

                [AttachmentSlot.Complaint] = new SlotRule
                {
                    AllowMultiple = true,
                    AllowedMimePrefixes = new[]
                    {
                        "image/",
                        "video/",
                        "application/pdf",
                        "application/msword",
                        "application/vnd.openxmlformats-officedocument"
                    },
                    MaxBytes = 50 * MB,
                    GenerateThumbnail = true
                },

                // Trao đổi nội bộ: nhận hầu hết loại file văn phòng và media phổ biến.
                // Không cho executable/script để tránh dùng chat như kênh phát tán file nguy hiểm.
                [AttachmentSlot.InternalMail] = new SlotRule
                {
                    AllowMultiple = true,
                    AllowedMimePrefixes = new[]
                    {
                        "image/",
                        "audio/",
                        "video/",
                        "text/",
                        "application/pdf",
                        "application/json",
                        "application/xml",
                        "application/msword",
                        "application/vnd.ms-excel",
                        "application/vnd.ms-powerpoint",
                        "application/vnd.ms-outlook",
                        "application/vnd.openxmlformats-officedocument",
                        "application/vnd.oasis.opendocument",
                        "application/zip",
                        "application/x-7z-compressed",
                        "application/x-rar-compressed"
                    },
                    MaxBytes = 50 * MB
                },
            };
    }

}
