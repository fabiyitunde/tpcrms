using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.LoanApplication.Commands;

public record UpdatePartyInfoCommand(
    Guid ApplicationId,
    Guid PartyId,
    string? BVN,
    decimal? ShareholdingPercent,
    Guid UpdatedByUserId
) : IRequest<ApplicationResult>;

public class UpdatePartyInfoHandler : IRequestHandler<UpdatePartyInfoCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePartyInfoHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(UpdatePartyInfoCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdWithPartiesAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Application not found");

        // Allow BVN/shareholding corrections through CreditAnalysis — data entry errors on BVNs
        // are common and must be fixable after submission so credit checks can be re-run.
        var allowedStatuses = new[] { "Draft", "Submitted", "DataGathering", "BranchReview", "BranchApproved", "CreditAnalysis" };
        if (!allowedStatuses.Contains(application.Status.ToString()))
            return ApplicationResult.Failure("Party information can only be updated before the application reaches HO Review.");

        var result = application.UpdatePartyFields(request.PartyId, request.BVN, request.ShareholdingPercent);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}
