using CRMS.Application.Common;
using CRMS.Application.CreditBureau.Interfaces;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.LoanApplication.Commands;

public record SubmitLoanApplicationCommand(Guid ApplicationId, Guid UserId) : IRequest<ApplicationResult>;

public class SubmitLoanApplicationHandler : IRequestHandler<SubmitLoanApplicationCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitLoanApplicationHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(SubmitLoanApplicationCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.Submit(request.UserId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ApproveBranchCommand(Guid ApplicationId, Guid UserId, string? Comment) : IRequest<ApplicationResult>;

public class ApproveBranchHandler : IRequestHandler<ApproveBranchCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICreditCheckQueue _creditCheckQueue;

    public ApproveBranchHandler(
        ILoanApplicationRepository repository, 
        IUnitOfWork unitOfWork,
        ICreditCheckQueue creditCheckQueue)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _creditCheckQueue = creditCheckQueue;
    }

    public async Task<ApplicationResult> Handle(ApproveBranchCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ApproveBranch(request.UserId, request.Comment);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        // Queue credit checks for background processing
        await _creditCheckQueue.QueueCreditCheckAsync(request.ApplicationId, request.UserId, ct);

        return ApplicationResult.Success();
    }
}

public record ReturnFromBranchCommand(Guid ApplicationId, Guid UserId, string Reason) : IRequest<ApplicationResult>;

public class ReturnFromBranchHandler : IRequestHandler<ReturnFromBranchCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnFromBranchHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ReturnFromBranchCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ReturnFromBranch(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}
