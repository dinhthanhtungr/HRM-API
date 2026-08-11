using HRM.Application.Features.Pdf.TestDocuments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRM.Api.Controllers.Pdf;

[ApiController]
[Authorize]
[Route("api/v1/pdf-tests")]
public sealed class PdfTestsController : ControllerBase
{
    private const string VietAusLogoFileName = "VietAusLogo.png";
    private readonly IWebHostEnvironment _environment;

    public PdfTestsController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet("vietaus-logo")]
    public async Task<IActionResult> GetVietAusLogoTest(CancellationToken cancellationToken)
    {
        var logoPath = Path.Combine(
            _environment.ContentRootPath,
            "Assets",
            "Pdf",
            VietAusLogoFileName);

        if (!System.IO.File.Exists(logoPath))
        {
            return NotFound(new
            {
                message = "PDF logo asset not found.",
                asset = VietAusLogoFileName
            });
        }

        var logoBytes = await System.IO.File.ReadAllBytesAsync(logoPath, cancellationToken);
        var pdfBytes = VietAusLogoTestPdf.Generate(logoBytes);

        return File(pdfBytes, "application/pdf", "vietaus-logo-test.pdf");
    }
}
