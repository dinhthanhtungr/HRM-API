namespace HRM.Infrastructure.Documents.Pdfs;

public sealed class PdfOptions
{
    public const string SectionName = "Pdf";

    public string QuestPdfLicense { get; init; } = "Evaluation";
    public QuotationPdfBrandingOptions Quotation { get; init; } = new();
}

public sealed class QuotationPdfBrandingOptions
{
    public string? LogoPath { get; init; }
    public string? BureauVeritasLogoPath { get; init; }
    public string? GrsLogoPath { get; init; }
    public string? QrCodePath { get; init; }

    public string? Factory01 { get; init; } = "No 296, Trung Thang Town, Dong Hoa ward, HCMC, Vietnam.";
    public string? Factory02 { get; init; } = "Doc 47 Industrial Cluster, Long Khanh 2 Town, Tam Phuoc Ward, Dong Nai City, Vietnam.";
    public string CompanyDisplayName { get; init; } = "VIETAUS POLYMER CO., LTD.";
    public string HeadOffice { get; init; } = "No 26/6, Street 12, Tam Binh ward, HCMC, Vietnam.";
    public string HeadOfficeContact { get; init; } = "T +84 28 73 09 39 69 | M +84 918 068 656 | F +84 274 3800 037";
    public string HeadOfficeEmail { get; init; } = "duy.nguyen@vietaus.com";
    public string HaNoiBranch { get; init; } = "No 11, Lane 7, Nghia Do st., Nghia Do ward, Ha Noi City, Vietnam.";
    public string HaNoiBranchContact { get; init; } = "T +84 24 32 191 132 | M +84 915 344 343 | F +84 24 32 191 132";
    public string HaNoiBranchEmail { get; init; } = "lam.quachthua@vietaus.com";
    public string DaNangBranch { get; init; } = "No 72, Dao Su Tich st., Hoa Khanh Dist, Da Nang City, Vietnam.";
    public string DaNangBranchContact { get; init; } = "T +84 236 355 0869 | M +84 938 636 588 | F +84 236 355 2869";
    public string DaNangBranchEmail { get; init; } = "hoaloc.nguyenthi@vietaus.com";
    public string Factory01Contact { get; init; } = "T +84 28 73 093 969 | M +84 938 679 588 | F +84 274 3 800 037";
    public string Factory01Email { get; init; } = "phuong.nguyenvan@vietaus.com";
    public string Factory02Contact { get; init; } = "T +84 28 73 093 969 | M +84 938 679 588 | F +84 274 3 800 037";
    public string Factory02Email { get; init; } = "phuong.nguyenvan@vietaus.com";
    public string Website { get; init; } = "https://vietaus.com";
    public string Hotline { get; init; } = "(84). 28. 730 93 969";
    public string Slogan { get; init; } =
        "COLOURING YOUR FUTURE WITH SERVICE AT YOUR DOORSTEP";

    public string IsoStandards { get; init; } = "ISO 9001/ISO 14001/ISO 45001";
    public string? FormCode { get; init; } = "VA-SA-F01(02)";
    public string? EffectiveDate { get; init; } = "26-10-2020";
}
