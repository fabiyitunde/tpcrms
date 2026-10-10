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
