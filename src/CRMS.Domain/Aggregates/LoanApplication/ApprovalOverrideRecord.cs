using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.LoanApplication;

public class ApprovalOverrideRecord : Entity
{
    public Guid LoanApplicationId { get; private set; }
    public string Stage { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public string ActorName { get; private set; } = string.Empty;
    public string NoteText { get; private set; } = string.Empty;
    public bool IsResolved { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? ResolvedByName { get; private set; }

    private ApprovalOverrideRecord() { }

    public static ApprovalOverrideRecord Create(
        Guid loanApplicationId,
        string stage,
        Guid actorId,
        string actorName,
        string noteText)
    {
        return new ApprovalOverrideRecord
        {
            LoanApplicationId = loanApplicationId,
            Stage = stage,
            ActorId = actorId,
            ActorName = actorName,
            NoteText = noteText,
            IsResolved = false
        };
    }

    public void MarkResolved(string resolvedByName)
    {
        IsResolved = true;
        ResolvedAt = DateTime.UtcNow;
        ResolvedByName = resolvedByName;
    }
}
