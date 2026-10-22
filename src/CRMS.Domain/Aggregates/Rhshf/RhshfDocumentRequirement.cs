using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Admin-configured "this document must be attached before a case can be submitted" rule.
///
/// The FAC profiling form previously had no required-document concept at all: a single uncategorised
/// file input, unlimited uploads, and nothing stopping submission with zero attachments. Credit
/// officers then discovered the gap at appraisal, after the case had already entered the pipeline.
///
/// Same template shape as RhshfPreDeploymentChecklistTemplate — admin-editable, mandatory flag,
/// sort order, soft-deactivated rather than deleted so historical cases stay explicable.
/// </summary>
public class RhshfDocumentRequirement : Entity
{
    public RhshfDocumentCategory Category { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsMandatory { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// Which formats this slot accepts, as comma-separated <see cref="RhshfDocumentFormat"/> names.
    /// <para>
    /// Empty means no restriction, which is how every row behaved before this rule existed — an
    /// un-backfilled requirement therefore keeps accepting whatever it accepted yesterday rather
    /// than suddenly rejecting the FAC's uploads.
    /// </para>
    /// </summary>
    public string? AllowedFormats { get; private set; }

    /// <summary>
    /// Whether an uploaded workbook must be the bank's financial statements template, rather than
    /// merely a spreadsheet. Set on the audited-financials slot: the officers' one-click import
    /// parses that file, and a workbook of the wrong shape fails at appraisal instead of at upload,
    /// by which time the FAC has gone.
    /// </summary>
    public bool MustMatchFinancialTemplate { get; private set; }

    /// <summary>The configured formats, or an empty list where the slot accepts anything. Unknown
    /// names are ignored rather than thrown: a rule nobody can parse must not block an upload.</summary>
    public IReadOnlyList<RhshfDocumentFormat> AllowedFormatList =>
        string.IsNullOrWhiteSpace(AllowedFormats)
            ? []
            : AllowedFormats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => Enum.TryParse<RhshfDocumentFormat>(t, ignoreCase: true, out var f)
                    ? (RhshfDocumentFormat?)f : null)
                .Where(f => f is not null)
                .Select(f => f!.Value)
                .Distinct()
                .ToList();

    protected RhshfDocumentRequirement() { }

    public static Result<RhshfDocumentRequirement> Create(
        RhshfDocumentCategory category, string title, string? description, bool isMandatory, int sortOrder,
        IEnumerable<RhshfDocumentFormat>? allowedFormats = null, bool mustMatchFinancialTemplate = false)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<RhshfDocumentRequirement>("Title is required.");

        return Result.Success(new RhshfDocumentRequirement
        {
            Category = category,
            Title = title.Trim(),
            Description = description?.Trim(),
            IsMandatory = isMandatory,
            SortOrder = sortOrder,
            IsActive = true,
            AllowedFormats = Join(allowedFormats),
            MustMatchFinancialTemplate = mustMatchFinancialTemplate,
        });
    }

    public Result Update(string title, string? description, bool isMandatory, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure("Title is required.");

        Title = title.Trim();
        Description = description?.Trim();
        IsMandatory = isMandatory;
        SortOrder = sortOrder;
        return Result.Success();
    }

    /// <summary>
    /// Sets what the slot will accept. Separate from <see cref="Update"/> so the existing admin
    /// screen and seeder keep working untouched, and so changing a format rule is a deliberate act
    /// rather than a side effect of renaming a requirement.
    /// </summary>
    public void SetFormatRules(IEnumerable<RhshfDocumentFormat>? allowedFormats, bool mustMatchFinancialTemplate)
    {
        AllowedFormats = Join(allowedFormats);
        MustMatchFinancialTemplate = mustMatchFinancialTemplate;
    }

    private static string? Join(IEnumerable<RhshfDocumentFormat>? formats)
    {
        var list = formats?.Distinct().ToList();
        return list is null || list.Count == 0 ? null : string.Join(",", list);
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
