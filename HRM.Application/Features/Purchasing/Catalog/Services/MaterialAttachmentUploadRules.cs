using HRM.Application.Features.Attachments.Dtos;
using HRM.Domain.Enums.Attachment;
using HRM.Domain.Security.Rules.Attachment;

namespace HRM.Application.Features.Purchasing.Catalog.Services;

internal static class MaterialAttachmentUploadRules
{
    internal static async Task ValidateAsync(AttachmentSlot slot, IReadOnlyList<AttachmentUploadFile> files, CancellationToken ct)
    {
        if (slot is not (AttachmentSlot.Photo or AttachmentSlot.MaterialTds or AttachmentSlot.MaterialMsds or
            AttachmentSlot.MaterialCoa or AttachmentSlot.MaterialCertificate or AttachmentSlot.MaterialOther))
            throw new InvalidOperationException("Slot tệp NVL không hợp lệ.");
        if (files.Count == 0) throw new InvalidOperationException("Chưa chọn tệp.");
        foreach (var file in files)
        {
            if (file.Length <= 0 || file.Length > AttachmentRules.Map[slot].MaxBytes || !file.Stream.CanSeek)
                throw new InvalidOperationException("Dung lượng hoặc stream tệp không hợp lệ.");
            var start = file.Stream.Position;
            var header = new byte[12];
            var count = await file.Stream.ReadAtLeastAsync(header, 12, false, ct);
            file.Stream.Position = start;
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var valid = (extension, file.ContentType.ToLowerInvariant()) switch
            {
                (".pdf", "application/pdf") => slot != AttachmentSlot.Photo && count >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
                (".jpg" or ".jpeg", "image/jpeg") => count >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255,
                (".png", "image/png") => count >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                (".gif", "image/gif") => count >= 6 && (header.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || header.AsSpan(0, 6).SequenceEqual("GIF89a"u8)),
                (".webp", "image/webp") => count >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
                (".bmp", "image/bmp") => count >= 2 && header[0] == 66 && header[1] == 77,
                _ => false
            };
            if (!valid) throw new InvalidOperationException("Chỉ nhận ảnh JPG/PNG/GIF/WebP/BMP hoặc PDF đúng định dạng.");
        }
    }
}
