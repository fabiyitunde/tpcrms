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

/// <summary>The answer to a submitted check: it has been stored and queued.</summary>
/// <param name="JobId">The job's id. Use it to fetch the status and the result.</param>
/// <param name="RequestId">The request id you sent.</param>
/// <param name="Status"><c>QUEUED</c>, <c>PROCESSING</c>, <c>COMPLETED</c> or <c>FAILED</c>. A repeated request id returns the existing job with whatever status it has reached.</param>
/// <param name="StatusUrl">The path to fetch this job from.</param>
/// <param name="QueuedAt">When the job was accepted (UTC).</param>
/// <param name="CallbackRegistered">True when a callback URL applies to this job.</param>
public record CustomerCheckAccepted(
    string JobId,
    string RequestId,
    string Status,
    string StatusUrl,
    DateTime QueuedAt,
    bool CallbackRegistered);

/// <summary>Why a check failed.</summary>
/// <param name="Code"><c>check_failed</c>, <c>timed_out</c>, <c>interrupted</c> or <c>consumer_inactive</c>.</param>
/// <param name="Message">What happened, in a sentence.</param>
public record CustomerCheckJobError(string Code, string Message);

/// <summary>Where the job's callback stands.</summary>
/// <param name="Status"><c>NOT_REQUESTED</c> (no callback URL), <c>PENDING</c>, <c>DELIVERED</c> or <c>FAILED</c> (every attempt was refused).</param>
/// <param name="Attempts">How many attempts have been made.</param>
/// <param name="DeliveredAt">When your endpoint acknowledged it (UTC).</param>
/// <param name="LastStatusCode">The HTTP status your endpoint last answered with.</param>
/// <param name="NextAttemptAt">When the next attempt is due (UTC).</param>
public record CustomerCheckCallbackState(
    string Status,
    int Attempts,
    DateTime? DeliveredAt,
    int? LastStatusCode,
    DateTime? NextAttemptAt);

/// <summary>A check's status and, once completed, its result.</summary>
/// <param name="JobId">The job's id.</param>
/// <param name="RequestId">The request id you sent.</param>
/// <param name="Status"><c>QUEUED</c>, <c>PROCESSING</c>, <c>COMPLETED</c> or <c>FAILED</c>.</param>
/// <param name="QueuedAt">When the job was accepted (UTC).</param>
/// <param name="StartedAt">When a worker started it (UTC).</param>
/// <param name="CompletedAt">When it finished (UTC).</param>
/// <param name="Sections">The sections asked for.</param>
/// <param name="Error">Present only when the job failed. Send the check again with a new request id; nothing is billed for a failed check.</param>
/// <param name="Callback">Where the callback stands.</param>
/// <param name="Result">The result, present only when completed. It is the object described as CustomerCheckResponse, exactly as it is sent to your callback.</param>
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

/// <summary>A search of your past checks. Every field is optional.</summary>
/// <param name="From">Checks queued on or after this date.</param>
/// <param name="To">Checks queued on or before this date.</param>
/// <param name="Status"><c>QUEUED</c>, <c>PROCESSING</c>, <c>COMPLETED</c> or <c>FAILED</c>.</param>
/// <param name="RequestId">An exact request id.</param>
/// <param name="Bvn">A BVN, 11 digits. Matched on its keyed hash; never stored or logged in plain form.</param>
/// <param name="Page">Page number, from 1. Default 1.</param>
/// <param name="PageSize">Items per page, at most 100. Default 50.</param>
public record CustomerCheckHistoryRequest(
    DateOnly? From,
    DateOnly? To,
    string? Status,
    string? RequestId,
    string? Bvn,
    int? Page,
    int? PageSize);

/// <summary>One past check, without its result.</summary>
/// <param name="JobId">The job's id; fetch the result with it.</param>
/// <param name="BvnLast4">The last four digits of the BVN checked.</param>
public record CustomerCheckHistoryItem(
    string JobId,
    string RequestId,
    string Status,
    DateTime QueuedAt,
    DateTime? CompletedAt,
    IReadOnlyList<string> Sections,
    string BvnLast4,
    CustomerCheckCallbackState Callback);

/// <summary>A page of past checks, newest first.</summary>
/// <param name="Total">How many checks match in all.</param>
public record CustomerCheckHistoryPage(int Page, int PageSize, int Total, IReadOnlyList<CustomerCheckHistoryItem> Items);

/// <summary>What is posted to the consumer's callback URL. <see cref="Result"/> is the reply as
/// stored, verbatim; it is not re-shaped on the way out.</summary>
/// <param name="EventType"><c>CUSTOMER_CHECK_COMPLETED</c> or <c>CUSTOMER_CHECK_FAILED</c>.</param>
/// <param name="EventId">Stable across every retry of the same event. If you have already processed it, answer 2xx and ignore the repeat.</param>
/// <param name="JobId">The job's id.</param>
/// <param name="RequestId">The request id you sent.</param>
/// <param name="Status"><c>COMPLETED</c> or <c>FAILED</c>.</param>
/// <param name="OccurredAt">When the check finished (UTC).</param>
/// <param name="StatusUrl">The path to fetch this job from.</param>
/// <param name="Error">Present only for a failed check.</param>
/// <param name="Result">The result, present only for a completed check.</param>
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
