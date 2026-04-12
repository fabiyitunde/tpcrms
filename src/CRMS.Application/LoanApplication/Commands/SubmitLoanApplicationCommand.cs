using CRMS.Application.Common;
using CRMS.Application.CreditBureau.Interfaces;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;

namespace CRMS.Application.LoanApplication.Commands;

public record SubmitLoanApplicationCommand(Guid ApplicationId, Guid UserId) : IRequest<ApplicationResult>;

public class SubmitLoanApplicationHandler : IRequestHandler<SubmitLoanApplicationCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IBankStatementRepository _statementRepository;
    private readonly WorkflowService _workflowService;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitLoanApplicationHandler(
        ILoanApplicationRepository repository,
        IBankStatementRepository statementRepository,
        WorkflowService workflowService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _statementRepository = statementRepository;
        _workflowService = workflowService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(SubmitLoanApplicationCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        // Bank statements are a separate aggregate — validate cross-aggregate requirement here.
        // Use HasStatementsAsync (AnyAsync) to avoid loading BankStatement entities into this
        // DbContext scope, which prevents false-positive change detection on those entities
        // during SaveChanges.
        var hasStatements = await _statementRepository.HasStatementsAsync(request.ApplicationId, ct);
        if (!hasStatements)
            return ApplicationResult.Failure("At least one bank statement is required");

        var result = application.Submit(request.UserId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        var branchReviewResult = application.SubmitForBranchReview(request.UserId);
        if (branchReviewResult.IsFailure)
            return ApplicationResult.Failure(branchReviewResult.Error);

        // Initialize the workflow instance so approval/return actions can function.
        // This runs in the same transaction as the status change — both are saved together below.
        var workflowResult = await _workflowService.InitializeWorkflowAsync(
            application.Id,
            application.Type,
            LoanApplicationStatus.BranchReview,
            request.UserId,
            ct);

        if (workflowResult.IsFailure)
            return ApplicationResult.Failure(workflowResult.Error);

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
    private readonly ICreditCheckOutbox _outbox;

    public ApproveBranchHandler(
        ILoanApplicationRepository repository,
        IUnitOfWork unitOfWork,
        ICreditCheckOutbox outbox)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _outbox = outbox;
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

        // Enqueue atomically: the outbox entry is added to the same DbContext change tracker
        // and committed in the single SaveChangesAsync below. If the transaction fails,
        // neither the approval nor the outbox entry is persisted — no gap, no lost checks.
        await _outbox.EnqueueAsync(application.Id, request.UserId, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ReturnFromHOReviewCommand(Guid ApplicationId, Guid UserId, string Reason) : IRequest<ApplicationResult>;

public class ReturnFromHOReviewHandler : IRequestHandler<ReturnFromHOReviewCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnFromHOReviewHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ReturnFromHOReviewCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ReturnFromHOReview(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record FinalApproveCommand(Guid ApplicationId, Guid UserId, string? Comment) : IRequest<ApplicationResult>;

public class FinalApproveHandler : IRequestHandler<FinalApproveCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public FinalApproveHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(FinalApproveCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.FinalApprove(request.UserId, request.Comment);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

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

public record MoveToCommitteeCommand(Guid ApplicationId, Guid UserId) : IRequest<ApplicationResult>;

public class MoveToCommitteeHandler : IRequestHandler<MoveToCommitteeCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public MoveToCommitteeHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(MoveToCommitteeCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.MoveToCommittee(request.UserId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ReturnFromCreditAnalysisCommand(Guid ApplicationId, Guid UserId, string Reason) : IRequest<ApplicationResult>;

public class ReturnFromCreditAnalysisHandler : IRequestHandler<ReturnFromCreditAnalysisCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnFromCreditAnalysisHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ReturnFromCreditAnalysisCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ReturnFromCreditAnalysis(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ApproveCreditAnalysisCommand(Guid ApplicationId, Guid UserId, string? Comment) : IRequest<ApplicationResult>;

public class ApproveCreditAnalysisHandler : IRequestHandler<ApproveCreditAnalysisCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveCreditAnalysisHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ApproveCreditAnalysisCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.MoveToHOReview(request.UserId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}
