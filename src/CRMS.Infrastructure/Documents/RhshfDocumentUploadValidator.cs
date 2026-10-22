using ClosedXML.Excel;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Infrastructure.Documents;

/// <summary>
/// Checks an uploaded supporting document against the format rules on its requirement.
/// <para>
/// Everything here is decided from the bytes. The filename extension and the content type the
/// browser sends are both supplied by the client and are routinely wrong even without anyone trying
/// — a file saved from an email, or renamed to get past a form, arrives mislabelled either way.
/// </para>
/// </summary>
public class RhshfDocumentUploadValidator : IRhshfDocumentUploadValidator
{
    /// <summary>
    /// Below this a file is a mistake rather than a document — an empty export, a failed scan, a
    /// placeholder. Deliberately low: a one-page text PDF is legitimately small.
    /// </summary>
    private const int MinSizeBytes = 1024;

    public Result Validate(RhshfDocumentRequirement? requirement, string fileName, byte[] content)
    {
        var name = string.IsNullOrWhiteSpace(fileName) ? "The file" : $"\"{fileName}\"";

        if (content.Length < MinSizeBytes)
            return Result.Failure(
                $"{name} is only {content.Length} bytes, which is too small to be a real document. " +
                "Check the file opened correctly before uploading it.");

        var allowed = requirement?.AllowedFormatList ?? [];
        if (allowed.Count == 0)
            return Result.Success();   // slot accepts anything; nothing further to check

        var detected = Detect(content);
        if (detected is null || !allowed.Contains(detected.Value))
            return Result.Failure(
                $"{name} is not {Describe(allowed)}. " +
                (detected is null
                    ? "The file does not look like any accepted format."
                    : $"It looks like {Article(detected.Value)}.") +
                " Upload the document in the expected format.");

        if (requirement!.MustMatchFinancialTemplate && detected == RhshfDocumentFormat.Spreadsheet)
            return ValidateFinancialTemplate(name, content);

        return Result.Success();
    }

    /// <summary>
    /// Confirms the workbook is the bank's financial statements template rather than any
    /// spreadsheet.
    /// <para>
    /// These are the same expectations FinancialStatementExcelService.ParseUploadedFile reads with,
    /// checked here so the FAC is told at upload instead of a credit officer discovering it when the
    /// one-click import fails. The two must change together.
    /// </para>
    /// </summary>
    private static Result ValidateFinancialTemplate(string name, byte[] content)
    {
        try
        {
            using var ms = new MemoryStream(content);
            using var workbook = new XLWorkbook(ms);

            var balance = workbook.Worksheets.FirstOrDefault(
                w => w.Name.Contains("Balance", StringComparison.OrdinalIgnoreCase));
            if (balance is null)
                return Result.Failure(
                    $"{name} has no Balance Sheet worksheet, so it is not the financial statements " +
                    "template. Download the template, fill it in, and upload that file.");

            var income = workbook.Worksheets.FirstOrDefault(
                w => w.Name.Contains("Income", StringComparison.OrdinalIgnoreCase)
                     || w.Name.Contains("P&L", StringComparison.OrdinalIgnoreCase));
            if (income is null)
                return Result.Failure(
                    $"{name} has no Income Statement worksheet, so it is not the financial statements " +
                    "template. Download the template, fill it in, and upload that file.");

            // Years live in row 3, columns C to E. Without at least one the import has nothing to
            // attach figures to, so an otherwise correct-looking template is still unusable.
            var hasYear = Enumerable.Range(3, 3)
                .Any(col => int.TryParse(balance.Cell(3, col).GetString(), out _));
            if (!hasYear)
                return Result.Failure(
                    $"{name} has no financial years filled in on the Balance Sheet (row 3, columns C to E). " +
                    "Enter the years the figures cover and upload it again.");

            return Result.Success();
        }
        catch (Exception ex)
        {
            // A corrupt or password-protected workbook lands here. The FAC cannot act on a library
            // stack trace, so say what to do instead.
            return Result.Failure(
                $"{name} could not be opened as a spreadsheet ({ex.GetType().Name}). " +
                "If it is password-protected, remove the protection and upload it again.");
        }
    }

    /// <summary>The format a file actually is, from its signature. Null where it matches none.</summary>
    private static RhshfDocumentFormat? Detect(byte[] c)
    {
        if (StartsWith(c, "%PDF-"u8)) return RhshfDocumentFormat.Pdf;
        if (StartsWith(c, [0x89, 0x50, 0x4E, 0x47])) return RhshfDocumentFormat.Image;          // PNG
        if (StartsWith(c, [0xFF, 0xD8, 0xFF])) return RhshfDocumentFormat.Image;                // JPEG
        if (StartsWith(c, [0x50, 0x4B, 0x03, 0x04])) return IsWorkbook(c) ? RhshfDocumentFormat.Spreadsheet : null;
        return null;
    }

    /// <summary>
    /// Whether a zip is specifically an .xlsx. Every Office format is a zip starting with the same
    /// four bytes, so without this a Word document would pass as a spreadsheet; only a workbook
    /// carries the "xl/" part.
    /// </summary>
    private static bool IsWorkbook(byte[] content)
    {
        try
        {
            using var ms = new MemoryStream(content);
            using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
            return zip.Entries.Any(e => e.FullName.StartsWith("xl/", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;   // truncated or not really a zip
        }
    }

    private static bool StartsWith(byte[] content, ReadOnlySpan<byte> signature)
        => content.Length >= signature.Length && content.AsSpan(0, signature.Length).SequenceEqual(signature);

    private static string Describe(IReadOnlyList<RhshfDocumentFormat> allowed)
    {
        // Each phrase carries its own article, so the list reads as English whichever formats a slot
        // happens to allow: "a PDF or an Excel workbook", never "a Excel workbook".
        var phrases = allowed.Select(Article).Distinct().ToList();
        return phrases.Count switch
        {
            1 => phrases[0],
            2 => $"{phrases[0]} or {phrases[1]}",
            _ => string.Join(", ", phrases.Take(phrases.Count - 1)) + $" or {phrases[^1]}",
        };
    }

    private static string Article(RhshfDocumentFormat format) => format switch
    {
        RhshfDocumentFormat.Pdf => "a PDF",
        RhshfDocumentFormat.Spreadsheet => "an Excel workbook (.xlsx)",
        RhshfDocumentFormat.Image => "an image",
        _ => $"a {format}",
    };
}
