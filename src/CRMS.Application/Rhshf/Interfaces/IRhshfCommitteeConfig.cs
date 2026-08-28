namespace CRMS.Application.Rhshf.Interfaces;

/// <summary>Single flat committee for v1 (design doc §6 #11) — no value-based tiers. Abstraction
/// over RhshfSettings so the Application layer never references CRMS.Infrastructure directly.</summary>
public interface IRhshfCommitteeConfig
{
    int RequiredVotes { get; }
    int MinimumApprovalVotes { get; }
}
