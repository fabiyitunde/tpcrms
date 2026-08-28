using CRMS.Application.Rhshf.Interfaces;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.ExternalServices.Rhshf;

public class RhshfCommitteeConfig : IRhshfCommitteeConfig
{
    private readonly RhshfSettings _settings;

    public RhshfCommitteeConfig(IOptions<RhshfSettings> settings)
    {
        _settings = settings.Value;
    }

    public int RequiredVotes => _settings.CommitteeRequiredVotes;
    public int MinimumApprovalVotes => _settings.CommitteeMinimumApprovalVotes;
}
