using System.Text.Json;

namespace CRMS.Application.CustomerCheck;

// The asynchronous side of the customer check (docs/Customer_Check.md, "The flow"): what a consumer
// gets back when a check is accepted, when it asks after one, when it lists its history, and what
// is posted to its callback.

public static class CustomerCheckEvents
{
    public const string Completed = "CUSTOMER_CHECK_COMPLETED";
    public const string Failed = "CUSTOMER_CHECK_FAILED";
}

public static class CustomerCheckErrors
{
    public const string CheckFailed = "check_failed";
    public const string TimedOut = "timed_out";
    public const string Interrupted = "interrupted";
    public const string ConsumerInactive = "consumer_inactive";
    public const string AuditUnavailable = "audit_unavailable";
}

public record CustomerCheckAccepted(
    string JobId,
    string RequestId,
    string Status,
    string StatusUrl,
    DateTime QueuedAt,
    bool CallbackRegistered);

public record CustomerCheckJobError(string Code, string Message);

public record CustomerCheckCallbackState(
    string Status,
    int Attempts,
    DateTime? DeliveredAt,
    int? LastStatusCode,
    DateTime? NextAttemptAt);

public record CustomerCheckJobView(
    string JobId,
    string RequestId,
    string Status,
    DateTime QueuedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<string> Sections,
    CustomerCheckJobError? Error,
    CustomerCheckCallbackState Callback,
    JsonElement? Result);

public record CustomerCheckHistoryRequest(
    DateOnly? From,
    DateOnly? To,
    string? Status,
    string? RequestId,
    string? Bvn,
    int? Page,
    int? PageSize);

public record CustomerCheckHistoryItem(
    string JobId,
    string RequestId,
    string Status,
    DateTime QueuedAt,
    DateTime? CompletedAt,
    IReadOnlyList<string> Sections,
    string BvnLast4,
    CustomerCheckCallbackState Callback);

public record CustomerCheckHistoryPage(int Page, int PageSize, int Total, IReadOnlyList<CustomerCheckHistoryItem> Items);

/// <summary>What is posted to the consumer's callback URL. <see cref="Result"/> is the reply as
/// stored, verbatim; it is not re-shaped on the way out.</summary>
public record CustomerCheckCallbackEnvelope(
    string EventType,
    string EventId,
    string JobId,
    string RequestId,
    string Status,
    DateTime OccurredAt,
    string StatusUrl,
    CustomerCheckJobError? Error,
    JsonElement? Result);

/// <summary>Encrypts what must not sit readable in the database: the queued request (it holds the
/// BVN), the stored result, and each consumer's callback signing secret.</summary>
public interface ICustomerCheckCipher
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}

/// <summary>Sends one signed callback attempt. Knows nothing of retries or scheduling.</summary>
public interface ICustomerCheckCallbackSender
{
    Task<CallbackSendResult> SendAsync(string url, string secret, string eventId, string eventType, string body, CancellationToken ct = default);
}

public record CallbackSendResult(bool Succeeded, int? StatusCode, string? Error);

/// <summary>Wakes the worker when a job is queued, so pickup does not wait for the next poll.</summary>
public interface ICustomerCheckJobSignal
{
    void Nudge();
}

/// <summary>Job rows as the consumer sees them.</summary>
public static class CustomerCheckJobMapping
{
    public static CustomerCheckAccepted Accepted(Domain.Aggregates.CustomerCheck.CustomerCheckJob job) => new(
        CustomerCheckJobIds.Format(job.Id), job.RequestId, job.Status, CustomerCheckJobIds.StatusPath(job.Id),
        job.CreatedAt, job.CallbackUrl is not null);

    public static CustomerCheckCallbackState Callback(Domain.Aggregates.CustomerCheck.CustomerCheckJob job) => new(
        job.CallbackStatus, job.CallbackAttempts, job.CallbackDeliveredAt, job.LastCallbackStatusCode, job.NextCallbackAt);

    public static CustomerCheckJobError? Error(Domain.Aggregates.CustomerCheck.CustomerCheckJob job) =>
        job.Status == Domain.Aggregates.CustomerCheck.CustomerCheckJob.Failed
            ? new CustomerCheckJobError(job.ErrorCode ?? CustomerCheckErrors.CheckFailed, job.ErrorMessage ?? "The check could not be completed.")
            : null;

    public static CustomerCheckJobView View(Domain.Aggregates.CustomerCheck.CustomerCheckJob job, JsonElement? result) => new(
        CustomerCheckJobIds.Format(job.Id), job.RequestId, job.Status, job.CreatedAt, job.StartedAt, job.CompletedAt,
        job.SectionList, Error(job), Callback(job), result);

    public static CustomerCheckHistoryItem HistoryItem(Domain.Aggregates.CustomerCheck.CustomerCheckJob job) => new(
        CustomerCheckJobIds.Format(job.Id), job.RequestId, job.Status, job.CreatedAt, job.CompletedAt,
        job.SectionList, job.BvnLast4, Callback(job));
}

public static class CustomerCheckJobIds
{
    /// <summary>Job ids travel as 32 hex characters, no hyphens.</summary>
    public static string Format(Guid id) => id.ToString("N");

    public static bool TryParse(string? text, out Guid id) =>
        Guid.TryParseExact(text?.Trim(), "N", out id) || Guid.TryParse(text?.Trim(), out id);

    public static string StatusPath(Guid id) => $"/api/v1/customer-check/{Format(id)}";
}
