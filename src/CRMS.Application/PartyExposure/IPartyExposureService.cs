using CRMS.Domain.Aggregates.PartyExposure;

namespace CRMS.Application.PartyExposure;

public interface IPartyExposureService
{
    /// <summary>Every CRMS facility the identifiers lead to. Read-only.</summary>
    Task<PartyExposureResponse> LookupAsync(string requestId, ExposureQuery query, CancellationToken ct = default);

    /// <summary>Keyed hash of an identifier, for the audit log. Never reversible to the identifier.</summary>
    string HashIdentifier(string value);

    Task RecordLookupAsync(PartyExposureLookupLog log, CancellationToken ct = default);

    Task AddBatchAsync(PartyExposureBatch batch, CancellationToken ct = default);
    Task<PartyExposureBatch?> GetBatchAsync(Guid batchId, CancellationToken ct = default);
    Task<PartyExposureBatch?> FindBatchByRequestIdAsync(string requestId, CancellationToken ct = default);
    Task SaveBatchAsync(PartyExposureBatch batch, CancellationToken ct = default);

    /// <summary>Marks any batch left queued or in progress by a restart as failed. Its identifiers
    /// were only ever held in memory, so it cannot be resumed.</summary>
    Task<int> FailInterruptedBatchesAsync(CancellationToken ct = default);
}

public class PartyExposureSettings
{
    public const string SectionName = "PartyExposure";

    /// <summary>Key for the audit hashes and party labels. Falls back to the RH-SHF callback signing
    /// secret when blank, so TEST works with what is already configured.</summary>
    public string HashKey { get; set; } = string.Empty;

    /// <summary>Requests must carry X-Portal-Timestamp and X-Portal-Signature, signed with the RH-SHF
    /// callback signing secret. Only switch off for local work.</summary>
    public bool RequireSignature { get; set; } = true;

    /// <summary>How old a signed request may be, either way, before it is refused as a replay.</summary>
    public int SignatureToleranceSeconds { get; set; } = 300;

    public int PerUserLookupsPerMinute { get; set; } = 60;
    public int OverallLookupsPerMinute { get; set; } = 600;
    public int MaxRelationships { get; set; } = 200;

    /// <summary>Time allowed for the core banking balance reads in a single lookup. Whatever has not
    /// answered by then is reported as UNAVAILABLE rather than holding the reply.</summary>
    public int LiveBalanceBudgetMs { get; set; } = 1500;

    public int BatchMaxSubjects { get; set; } = 500;
    /// <summary>Subjects per database round in a batch, with a pause between, so a batch never
    /// crowds out live work.</summary>
    public int BatchChunkSize { get; set; } = 25;
    public int BatchChunkPauseMs { get; set; } = 200;

    /// <summary>Hosts a batch callback may be posted to. Blank means the host of Rhshf:PortalUrl.</summary>
    public List<string> AllowedCallbackHosts { get; set; } = [];
}
