using ClosedXML.Excel;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Documents;
using Xunit;

namespace CRMS.Infrastructure.Tests.Documents;

/// <summary>
/// What a document slot will accept.
///
/// Uploads were checked for nothing but "not empty" and "under 10 MB", which is how a PDF came to be
/// filed as audited financials and a plain text file as supporting evidence. None of it surfaced
/// until a credit officer opened the case, by which time the FAC had gone and the case had to be
/// returned for something the form could have caught in the first place.
///
/// Every decision here is made on the bytes. The extension and the browser-supplied content type are
/// client-controlled and routinely wrong even with nobody trying, so neither is evidence.
/// </summary>
public class RhshfDocumentUploadValidatorTests
{
    private readonly RhshfDocumentUploadValidator _validator = new();

    // ── Format ────────────────────────────────────────────────────────────

    [Fact]
    public void APdfIsAcceptedWhereAPdfIsAskedFor()
    {
        var result = _validator.Validate(Requirement([RhshfDocumentFormat.Pdf]), "resolution.pdf", Pdf());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ASpreadsheetRenamedAsAPdfIsStillRejected()
    {
        // The whole point of reading the signature: the name says PDF, the bytes say workbook.
        var result = _validator.Validate(Requirement([RhshfDocumentFormat.Pdf]), "resolution.pdf", Workbook());

        Assert.True(result.IsFailure);
        Assert.Contains("not a PDF", result.Error);
    }

    [Fact]
    public void APdfIsRejectedOnTheFinancialStatementsSlot()
    {
        // Exactly what happened in production: audited financials attached as a PDF, discovered when
        // the officer's import had nothing to parse.
        var result = _validator.Validate(FinancialsRequirement(), "accounts.pdf", Pdf());

        Assert.True(result.IsFailure);
        Assert.Contains("an Excel workbook", result.Error);
    }

    [Fact]
    public void APhotographIsAcceptedForLandDocumentation()
    {
        // Land title and warehousing evidence are routinely photographed; refusing images would make
        // the form fight the people filling it.
        var requirement = Requirement([RhshfDocumentFormat.Pdf, RhshfDocumentFormat.Image]);

        Assert.True(_validator.Validate(requirement, "title.png", Png()).IsSuccess);
        Assert.True(_validator.Validate(requirement, "title.pdf", Pdf()).IsSuccess);
    }

    [Fact]
    public void APlainTextFileIsRejectedOutright()
    {
        var result = _validator.Validate(Requirement([RhshfDocumentFormat.Pdf]), "notes.txt", Text());

        Assert.True(result.IsFailure);
        Assert.Contains("does not look like any accepted format", result.Error);
    }

    [Fact]
    public void AWordDocumentDoesNotPassAsASpreadsheet()
    {
        // Every Office format is a zip with the same four opening bytes, so the signature alone is
        // not enough — only a workbook carries the "xl/" part.
        var result = _validator.Validate(Requirement([RhshfDocumentFormat.Spreadsheet]), "register.docx", WordLikeZip());

        Assert.True(result.IsFailure);
    }

    // ── The financial statements template ─────────────────────────────────

    [Fact]
    public void TheFilledTemplateIsAccepted()
    {
        var result = _validator.Validate(FinancialsRequirement(), "financials.xlsx", TemplateWorkbook());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void AWorkbookThatIsNotTheTemplateIsRejected()
    {
        var result = _validator.Validate(FinancialsRequirement(), "my-accounts.xlsx", Workbook());

        Assert.True(result.IsFailure);
        Assert.Contains("Balance Sheet", result.Error);
    }

    [Fact]
    public void TheTemplateWithNoYearsFilledInIsRejected()
    {
        // Right shape, no content the import can attach figures to — an empty template downloaded
        // and uploaded straight back.
        var result = _validator.Validate(FinancialsRequirement(), "financials.xlsx", TemplateWorkbook(withYears: false));

        Assert.True(result.IsFailure);
        Assert.Contains("no financial years", result.Error);
    }

    [Fact]
    public void AWorkbookMissingOnlyTheIncomeStatementIsRejected()
    {
        var result = _validator.Validate(
            FinancialsRequirement(), "financials.xlsx", TemplateWorkbook(withIncome: false));

        Assert.True(result.IsFailure);
        Assert.Contains("Income Statement", result.Error);
    }

    // ── Size, and slots with no rule ──────────────────────────────────────

    [Fact]
    public void AFileTooSmallToBeADocumentIsRejectedWhateverTheSlot()
    {
        // A failed scan or an empty export. Caught even where the slot accepts anything.
        var result = _validator.Validate(null, "scan.pdf", new byte[40]);

        Assert.True(result.IsFailure);
        Assert.Contains("too small", result.Error);
    }

    [Fact]
    public void ASlotWithNoConfiguredFormatsAcceptsAnything()
    {
        // An un-backfilled requirement must keep behaving the way it did yesterday rather than
        // suddenly rejecting the FAC's uploads.
        Assert.True(_validator.Validate(Requirement([]), "anything.txt", Text()).IsSuccess);
        Assert.True(_validator.Validate(null, "anything.txt", Text()).IsSuccess);
    }

    // ── Fixtures ──────────────────────────────────────────────────────────

    private static RhshfDocumentRequirement Requirement(RhshfDocumentFormat[] formats) =>
        RhshfDocumentRequirement.Create(
            RhshfDocumentCategory.BoardResolution, "Board Resolution", null, true, 10, formats).Value;

    private static RhshfDocumentRequirement FinancialsRequirement() =>
        RhshfDocumentRequirement.Create(
            RhshfDocumentCategory.AuditedFinancials, "Audited Financial Statements", null, true, 20,
            [RhshfDocumentFormat.Spreadsheet], mustMatchFinancialTemplate: true).Value;

    private static byte[] Pdf() => Pad("%PDF-1.7\n% a perfectly ordinary pdf\n"u8.ToArray());
    private static byte[] Png() => Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
    private static byte[] Text() => Pad("Dear Sir, please find the document attached.\n"u8.ToArray());

    private static byte[] Pad(byte[] head)
    {
        // Past the size floor, so these tests exercise format rather than size.
        var padded = new byte[4096];
        head.CopyTo(padded, 0);
        return padded;
    }

    /// <summary>A real .xlsx, but not the template — one unremarkable sheet.</summary>
    private static byte[] Workbook()
    {
        using var wb = new XLWorkbook();
        wb.AddWorksheet("Sheet1").Cell(1, 1).Value = "some figures";
        return Save(wb);
    }

    private static byte[] TemplateWorkbook(bool withIncome = true, bool withYears = true)
    {
        using var wb = new XLWorkbook();
        var balance = wb.AddWorksheet("Balance Sheet");
        if (withYears)
        {
            balance.Cell(3, 3).Value = "2024";
            balance.Cell(3, 4).Value = "2025";
            balance.Cell(3, 5).Value = "2026";
        }

        if (withIncome)
            wb.AddWorksheet("Income Statement").Cell(1, 1).Value = "Revenue";

        return Save(wb);
    }

    /// <summary>A zip with Word's part layout — same opening bytes as a workbook, no "xl/".</summary>
    private static byte[] WordLikeZip()
    {
        using var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            using var entry = zip.CreateEntry("word/document.xml").Open();
            entry.Write("<document/>"u8);
        }

        return Pad(ms.ToArray());
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
