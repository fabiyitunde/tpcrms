namespace CRMS.Application.Workflow.Interfaces;

public interface IApprovalGateConfig
{
    bool IsStrict(string stage);
}
