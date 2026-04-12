namespace CRMS.Application.CreditBureau.Interfaces;

/// <summary>
/// Adds a credit check outbox entry to the ambient unit of work WITHOUT saving.
/// The caller is responsible for committing via SaveChangesAsync, which persists
/// both the triggering state change and the outbox entry in a single atomic transaction.
/// </summary>
public interface ICreditCheckOutbox
{
    Task EnqueueAsync(Guid loanApplicationId, Guid systemUserId, CancellationToken ct = default);
}
