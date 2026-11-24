using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Aggregates.CustomerCheck;

namespace CRMS.Application.CustomerCheck;

/// <summary>
/// Who a check runs for: a partner (an <see cref="ApiConsumer"/>) or a member of staff. The check
/// reads only these facts, so the same sections serve both without the staff path needing a fake
/// consumer row.
/// </summary>
/// <param name="Name">Shown in the result as the party the check ran for.</param>
/// <param name="AllowedSections">The sections this requester may run; any other requested one comes back NOT_ALLOWED.</param>
/// <param name="ReuseBureauReports">Serve a stored bureau report newer than <paramref name="ReuseMaxAgeDays"/> instead of billing a new pull.</param>
/// <param name="StoreBureauReports">Keep a fresh bureau report for later reuse. Partners only: staff checks never add to the store partners reuse from.</param>
/// <param name="FetchedById">Recorded on a stored bureau report as whoever fetched it.</param>
/// <param name="BillingFlow">The flow every third-party call is billed under.</param>
/// <param name="BillingReference">The reference those billing rows carry.</param>
/// <param name="TriggeredBy">The staff member who ran it, for the billing rows; null for a partner.</param>
public sealed record CustomerCheckRequester(
    string Name,
    IReadOnlySet<string> AllowedSections,
    bool ReuseBureauReports,
    int ReuseMaxAgeDays,
    bool StoreBureauReports,
    Guid FetchedById,
    BillingFlow BillingFlow,
    string BillingReference,
    Guid? TriggeredBy = null)
{
    public bool Allows(string section) => AllowedSections.Contains(section);

    /// <summary>The requester a partner's check has always run as: its own entitlements and reuse
    /// policy, billed to the Partner API under "{consumer} · {requestId}".</summary>
    public static CustomerCheckRequester ForConsumer(ApiConsumer consumer, string requestId) => new(
        consumer.Name,
        CustomerCheckSections.All.Where(consumer.Allows).ToHashSet(),
        consumer.ReuseBureauReports,
        consumer.BureauReuseMaxAgeDays,
        StoreBureauReports: true,
        consumer.Id,
        BillingFlow.PartnerApi,
        PartnerReference(consumer.Name, requestId));

    public static string PartnerReference(string consumerName, string requestId) => $"{consumerName} · {requestId}";
}
