using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>A free-text note any actor can add to a case — the shared discussion thread, mirroring
/// Corporate's Comments tab. Standalone and append-only: comments are never edited or deleted, and
/// are scoped to the profile (not a cycle), so the whole conversation stays visible across cycles.</summary>
public class RhshfComment : AggregateRoot
{
    public Guid RhshfCreditProfileId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Content { get; private set; } = string.Empty;

    protected RhshfComment() { }

    public static Result<RhshfComment> Create(Guid rhshfCreditProfileId, Guid authorUserId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Result.Failure<RhshfComment>("Comment cannot be empty.");
        if (authorUserId == Guid.Empty)
            return Result.Failure<RhshfComment>("Author is required.");

        var trimmed = content.Trim();
        return Result.Success(new RhshfComment
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            AuthorUserId = authorUserId,
            Content = trimmed.Length > 4000 ? trimmed[..4000] : trimmed,
        });
    }
}
