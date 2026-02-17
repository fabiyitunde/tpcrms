namespace CRMS.Application.CreditBureau.Interfaces;

/// <summary>
/// Abstraction for queuing credit checks for background processing.
/// </summary>
public interface ICreditCheckQueue
{
    /// <summary>
    /// Queues credit checks for all parties of a loan application.
    /// </summary>
    ValueTask QueueCreditCheckAsync(Guid loanApplicationId, Guid systemUserId, CancellationToken ct = default);
}
