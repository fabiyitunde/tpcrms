using CRMS.Domain.Aggregates.CustomerCheck;

namespace CRMS.Application.CustomerCheck;

public interface ICustomerCheckService
{
    /// <summary>Runs the sections asked for, each with its own timeout, and never lets one section's
    /// failure blank another. Read-only against every provider.</summary>
    Task<CustomerCheckOutcome> CheckAsync(ApiConsumer consumer, CustomerCheckQuery query, CancellationToken ct = default);

    /// <summary>Keyed hash of an identifier, for the audit log and the reuse store. Never reversible.</summary>
    string HashIdentifier(string value);

    Task RecordAsync(CustomerCheckLog log, CancellationToken ct = default);
}
