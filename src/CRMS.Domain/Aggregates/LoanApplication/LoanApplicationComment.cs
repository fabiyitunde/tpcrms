using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.LoanApplication;

public class LoanApplicationComment : Entity
{
    public Guid LoanApplicationId { get; private set; }
    public Guid UserId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? Category { get; private set; }

    private LoanApplicationComment() { }

    public LoanApplicationComment(Guid loanApplicationId, Guid userId, string content, string? category = null)
    {
        LoanApplicationId = loanApplicationId;
        UserId = userId;
        Content = content;
        Category = category;
    }
}
