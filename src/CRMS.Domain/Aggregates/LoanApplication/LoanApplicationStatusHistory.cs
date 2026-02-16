using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.LoanApplication;

public class LoanApplicationStatusHistory : Entity
{
    public Guid LoanApplicationId { get; private set; }
    public LoanApplicationStatus Status { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public string? Comment { get; private set; }
    public DateTime ChangedAt { get; private set; }

    private LoanApplicationStatusHistory() { }

    public LoanApplicationStatusHistory(
        Guid loanApplicationId,
        LoanApplicationStatus status,
        Guid changedByUserId,
        string? comment)
    {
        LoanApplicationId = loanApplicationId;
        Status = status;
        ChangedByUserId = changedByUserId;
        Comment = comment;
        ChangedAt = DateTime.UtcNow;
    }
}
