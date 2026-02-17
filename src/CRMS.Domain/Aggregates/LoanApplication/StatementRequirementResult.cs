namespace CRMS.Domain.Aggregates.LoanApplication;

/// <summary>
/// Result of validating bank statement requirements for a loan application.
/// </summary>
public class StatementRequirementResult
{
    public bool MeetsMinimumRequirements { get; set; }
    public bool HasInternalStatement { get; set; }
    public bool HasExternalStatements { get; set; }
    public bool AllExternalVerified { get; set; }
    public int TotalMonthsCovered { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public bool HasWarnings => Warnings.Count > 0;
    public bool HasErrors => Errors.Count > 0;

    public string Summary => MeetsMinimumRequirements
        ? HasWarnings 
            ? $"Requirements met with {Warnings.Count} warning(s)"
            : "All statement requirements met"
        : $"Requirements not met: {string.Join("; ", Errors)}";
}
