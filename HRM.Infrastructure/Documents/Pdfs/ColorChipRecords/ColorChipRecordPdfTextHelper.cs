using System;
using HRM.Application.Features.PLM.ColorChipRecords.Dtos;
using HRM.Domain.Enums.SampleRequests;

namespace HRM.Infrastructure.Documents.Pdfs.ColorChipRecords
{
    internal static class ColorChipRecordPdfTextHelper
    {
        public static string BuildApprovalText(LogoType logoType, bool lineBreak = false)
        {
            var separator = lineBreak ? "\n" : " ";
            return $"PLEASE RETURN ONE/TWO SETS TO {GetApprovalCompanyName(logoType)}{separator}UPON APPROVAL";
        }

        public static string ResolveApprovalText(ColorChipRecordPdfModel model, bool lineBreak = false)
        {
            if (!string.IsNullOrWhiteSpace(model.ApprovalText) &&
                !IsDefaultApprovalText(model.ApprovalText))
            {
                return model.ApprovalText;
            }

            return BuildApprovalText(ParseLogoType(model.LogoTypeText), lineBreak);
        }

        public static LogoType ParseLogoType(string? logoTypeText)
        {
            if (string.IsNullOrWhiteSpace(logoTypeText))
                return LogoType.Vietaus;

            return Enum.TryParse<LogoType>(logoTypeText, true, out var result)
                ? result
                : LogoType.Vietaus;
        }

        public static string GetApprovalCompanyName(LogoType logoType)
        {
            return logoType switch
            {
                LogoType.Vietaus => "VIETAUS POLYMER",
                LogoType.AChau => "A CHAU",
                LogoType.LongGiang => "LONG GIANG",
                LogoType.Others => "COMPANY",
                _ => "VIETAUS POLYMER"
            };
        }

        private static bool IsDefaultApprovalText(string approvalText)
        {
            return approvalText.Contains("PLEASE RETURN ONE/TWO SETS TO", StringComparison.OrdinalIgnoreCase)
                   && approvalText.Contains("UPON APPROVAL", StringComparison.OrdinalIgnoreCase);
        }
    }
}
