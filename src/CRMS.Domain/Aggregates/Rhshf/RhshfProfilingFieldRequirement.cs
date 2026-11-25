using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// Admin-configured "this item must be filled in before a case can be submitted" rule for profiling
/// data that is not a document — a director's NIN, a manager's phone number, a named Chief Promoter.
/// The document counterpart is <see cref="RhshfDocumentRequirement"/>.
///
/// One row per <see cref="RhshfProfilingFieldKey"/>, seeded add-missing. BOA raised these items
/// "subject to confirmation of their mandatory status", so each one is a switch an administrator
/// can turn off without a deploy. The director's BVN is not here: the bureau check is run on it,
/// so it stays required regardless.
/// </summary>
public class RhshfProfilingFieldRequirement : Entity
{
    public RhshfProfilingFieldKey Key { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsMandatory { get; private set; }
    public int SortOrder { get; private set; }

    protected RhshfProfilingFieldRequirement() { }

    public static Result<RhshfProfilingFieldRequirement> Create(
        RhshfProfilingFieldKey key, string title, string? description, bool isMandatory, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<RhshfProfilingFieldRequirement>("Title is required.");

        return Result.Success(new RhshfProfilingFieldRequirement
        {
            Key = key,
            Title = title.Trim(),
            Description = description?.Trim(),
            IsMandatory = isMandatory,
            SortOrder = sortOrder,
        });
    }

    public void SetMandatory(bool isMandatory) => IsMandatory = isMandatory;
}
