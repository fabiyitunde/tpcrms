using CRMS.Domain.Aggregates.CustomerCheck;

namespace CRMS.Domain.Interfaces;

public interface IApiConsumerRepository
{
    Task<ApiConsumer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiConsumer?> GetByKeyHashAsync(string keyHash, CancellationToken ct = default);
    Task<ApiConsumer?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<List<ApiConsumer>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(ApiConsumer consumer, CancellationToken ct = default);
    void Update(ApiConsumer consumer);
}

public interface ICustomerCheckJobRepository
{
    Task AddAsync(CustomerCheckJob job, CancellationToken ct = default);
    Task<CustomerCheckJob?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>The consumer's job with this request id queued on or after <paramref name="since"/>.</summary>
    Task<CustomerCheckJob?> FindByRequestIdAsync(Guid consumerId, string requestId, DateTime since, CancellationToken ct = default);
    /// <summary>Jobs of the consumer that are queued or running.</summary>
    Task<int> CountOpenAsync(Guid consumerId, CancellationToken ct = default);
    /// <summary>Atomically takes the next runnable job: the oldest queued one, or one left
    /// PROCESSING since before <paramref name="staleBefore"/> by a worker that died. Marks it
    /// PROCESSING as of <paramref name="now"/> and counts the attempt. Null when there is none.</summary>
    Task<Guid?> ClaimNextAsync(DateTime now, DateTime staleBefore, CancellationToken ct = default);
    /// <summary>Finished jobs whose callback is due.</summary>
    Task<List<Guid>> GetDueCallbackIdsAsync(DateTime now, int take, CancellationToken ct = default);
    Task<(List<CustomerCheckJob> Items, int Total)> SearchAsync(CustomerCheckJobFilter filter, CancellationToken ct = default);
    void Update(CustomerCheckJob job);
}

/// <summary>History search. Null means "any". Pages are 1-based.</summary>
public record CustomerCheckJobFilter(
    Guid? ConsumerId,
    DateTime? From,
    DateTime? To,
    string? Status,
    string? RequestId,
    string? BvnHash,
    int Page,
    int PageSize);

public interface ICustomerCheckPriceRepository
{
    Task<List<CustomerCheckPrice>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(CustomerCheckPrice price, CancellationToken ct = default);
    void Update(CustomerCheckPrice price);
}

public interface ICustomerCheckRepository
{
    Task AddLogAsync(CustomerCheckLog log, CancellationToken ct = default);
    /// <summary>The newest stored report for this person from this bureau, fetched on or after
    /// <paramref name="notBefore"/>; null when there is none.</summary>
    Task<CustomerBureauReport?> FindBureauReportAsync(string bvnHash, string bureau, DateTime notBefore, CancellationToken ct = default);
    Task AddBureauReportAsync(CustomerBureauReport report, CancellationToken ct = default);
}
