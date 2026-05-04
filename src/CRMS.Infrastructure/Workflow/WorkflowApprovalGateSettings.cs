using CRMS.Application.Workflow.Interfaces;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.Workflow;

public class WorkflowApprovalGateSettings
{
    public const string SectionName = "WorkflowApprovalGates";

    public StageGateSettings BranchReview { get; set; } = new();
    public StageGateSettings HOReview { get; set; } = new();
    public StageGateSettings CreditAnalysis { get; set; } = new();
    public StageGateSettings FinalApproval { get; set; } = new();
}

public class StageGateSettings
{
    public bool StrictApprovalGate { get; set; } = false;
}

public class ApprovalGateConfig : IApprovalGateConfig
{
    private readonly WorkflowApprovalGateSettings _settings;

    public ApprovalGateConfig(IOptions<WorkflowApprovalGateSettings> settings)
    {
        _settings = settings.Value;
    }

    public bool IsStrict(string stage) => stage switch
    {
        "BranchReview"   => _settings.BranchReview.StrictApprovalGate,
        "HOReview"       => _settings.HOReview.StrictApprovalGate,
        "CreditAnalysis" => _settings.CreditAnalysis.StrictApprovalGate,
        "FinalApproval"  => _settings.FinalApproval.StrictApprovalGate,
        _                => false
    };
}
