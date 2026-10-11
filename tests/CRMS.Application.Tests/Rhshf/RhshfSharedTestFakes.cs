using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// Legal clearance moved ahead of risk review, committee voting and ratification, so all three of
/// those handlers now read it to keep the Legal Officer out of their own distinct-actor checks.
/// Shared here rather than re-declared in each test class.
/// </summary>
internal class SharedFakeLegalClearanceRepository : IRhshfLegalClearanceRepository
{
    private readonly List<RhshfLegalClearance> _clearances;

    public SharedFakeLegalClearanceRepository(params RhshfLegalClearance[] clearances)
        => _clearances = [.. clearances];

    /// <summary>Convenience for the common case: a cycle cleared by this Legal Officer.</summary>
    public static SharedFakeLegalClearanceRepository ClearedBy(Guid profileId, int cycleNumber, Guid legalOfficerId)
        => new(RhshfLegalClearance.Create(
            profileId, cycleNumber, legalOfficerId, Domain.Enums.RhshfLegalClearanceOutcome.Granted, null).Value);

    public Task<IReadOnlyList<RhshfLegalClearance>> GetByProfileAndCycleAsync(
        Guid rhshfCreditProfileId, int cycleNumber, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RhshfLegalClearance>>(
            [.. _clearances.Where(c => c.RhshfCreditProfileId == rhshfCreditProfileId && c.CycleNumber == cycleNumber)]);

    public Task AddAsync(RhshfLegalClearance clearance, CancellationToken ct = default)
    {
        _clearances.Add(clearance);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Collateral reaches the committee vote handler because an approving vote has to answer for
/// unperfected security. The default is one perfected instrument — i.e. the question never arises —
/// so tests about other things stay about those things.
/// </summary>
internal class SharedFakeCollateralRepository : IRhshfCollateralRepository
{
    private readonly List<RhshfCollateral> _collateral;

    public SharedFakeCollateralRepository(params RhshfCollateral[] collateral) => _collateral = [.. collateral];

    /// <summary>One instrument, perfected, with its evidence on file.</summary>
    public static SharedFakeCollateralRepository Perfected(Guid profileId, int cycleNumber)
        => new(Instrument(profileId, cycleNumber, perfected: true));

    /// <summary>One instrument the Legal Officer did not perfect.</summary>
    public static SharedFakeCollateralRepository NotPerfected(Guid profileId, int cycleNumber)
        => new(Instrument(profileId, cycleNumber, perfected: false));

    private static RhshfCollateral Instrument(Guid profileId, int cycleNumber, bool perfected)
    {
        var collateral = RhshfCollateral.Create(
            profileId, cycleNumber, Domain.Enums.RhshfCollateralType.BankGuarantee, Guid.NewGuid(), null,
            "BG-2026-01", null, null, "First Bank", 52_000_000m, true, null, null, null, null, null).Value;
        collateral.AddDocument("guarantee.pdf", "application/pdf", "path/g.pdf", 1024);
        if (perfected)
            collateral.AssertPerfection(Guid.NewGuid(), Domain.Enums.RhshfCollateralPerfectionStatus.Perfected);
        return collateral;
    }

    public Task<IReadOnlyList<RhshfCollateral>> GetByProfileAndCycleAsync(Guid profileId, int cycleNumber, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RhshfCollateral>>(
            [.. _collateral.Where(c => c.RhshfCreditProfileId == profileId && c.CycleNumber == cycleNumber)]);

    public Task<RhshfCollateral?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_collateral.FirstOrDefault(c => c.Id == id));

    public Task AddAsync(RhshfCollateral collateral, CancellationToken ct = default)
    {
        _collateral.Add(collateral);
        return Task.CompletedTask;
    }

    public void Remove(RhshfCollateral collateral) => _collateral.Remove(collateral);

    public Task<RhshfCollateralDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
        => throw new NotSupportedException();
}

/// <summary>Offer acceptance reads the committee's perfection disposition to decide whether to add the
/// blocking gate item, so that handler needs the review.</summary>
internal class SharedFakeCommitteeReviewRepository : IRhshfCommitteeReviewRepository
{
    private RhshfCommitteeReview? _review;

    public SharedFakeCommitteeReviewRepository(RhshfCommitteeReview? review = null) => _review = review;

    public Task<RhshfCommitteeReview?> GetByProfileAndCycleAsync(Guid profileId, int cycleNumber, CancellationToken ct = default)
        => Task.FromResult(_review is not null && _review.RhshfCreditProfileId == profileId && _review.CycleNumber == cycleNumber ? _review : null);

    public Task AddAsync(RhshfCommitteeReview review, CancellationToken ct = default)
    {
        _review = review;
        return Task.CompletedTask;
    }
}

/// <summary>The pre-deployment gate checklist is seeded at offer acceptance now, so that handler
/// needs the template list.</summary>
internal class SharedFakeTemplateRepository : IRhshfPreDeploymentChecklistTemplateRepository
{
    private readonly List<RhshfPreDeploymentChecklistTemplate> _templates;

    public SharedFakeTemplateRepository(params RhshfPreDeploymentChecklistTemplate[] templates)
        => _templates = [.. templates];

    public Task<IReadOnlyList<RhshfPreDeploymentChecklistTemplate>> GetActiveAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RhshfPreDeploymentChecklistTemplate>>(_templates);

    public Task<IReadOnlyList<RhshfPreDeploymentChecklistTemplate>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RhshfPreDeploymentChecklistTemplate>>(_templates);

    public Task<RhshfPreDeploymentChecklistTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_templates.FirstOrDefault(t => t.Id == id));

    public Task AddAsync(RhshfPreDeploymentChecklistTemplate template, CancellationToken ct = default)
    {
        _templates.Add(template);
        return Task.CompletedTask;
    }

    public void Update(RhshfPreDeploymentChecklistTemplate template) { }
}
