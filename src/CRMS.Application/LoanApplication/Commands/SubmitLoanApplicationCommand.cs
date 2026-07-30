using CRMS.Application.Common;
using CRMS.Application.CreditBureau.Interfaces;
using CRMS.Application.StatementAnalysis.Commands;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;
using CRMS.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace CRMS.Application.LoanApplication.Commands;

public record SubmitLoanApplicationCommand(Guid ApplicationId, Guid UserId) : IRequest<ApplicationResult>;

public class SubmitLoanApplicationHandler : IRequestHandler<SubmitLoanApplicationCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IBankStatementRepository _statementRepository;
    private readonly IFinancialStatementRepository _financialStatementRepository;
    private readonly WorkflowService _workflowService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AnalyzeStatementHandler _analyzeStatementHandler;
    private readonly ICreditCheckOutbox _outbox;
    private readonly ILogger<SubmitLoanApplicationHandler> _logger;

    public SubmitLoanApplicationHandler(
        ILoanApplicationRepository repository,
        IBankStatementRepository statementRepository,
        IFinancialStatementRepository financialStatementRepository,
        WorkflowService workflowService,
        IUnitOfWork unitOfWork,
        AnalyzeStatementHandler analyzeStatementHandler,
        ICreditCheckOutbox outbox,
        ILogger<SubmitLoanApplicationHandler> logger)
    {
        _repository = repository;
        _statementRepository = statementRepository;
        _financialStatementRepository = financialStatementRepository;
        _workflowService = workflowService;
        _unitOfWork = unitOfWork;
        _analyzeStatementHandler = analyzeStatementHandler;
        _outbox = outbox;
        _logger = logger;
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

        var result = application.SubmitForCreditReview(request.UserId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        // Auto-submit all Draft financial statements so they enter PendingReview
        var financialStatements = await _financialStatementRepository.GetByLoanApplicationIdAsync(request.ApplicationId, ct);
        foreach (var fs in financialStatements.Where(f => f.Status == Domain.Enums.FinancialStatementStatus.Draft))
        {
            fs.Submit();
            _financialStatementRepository.Update(fs);
        }

        // Initialize the workflow instance so approval/return actions can function.
        // This runs in the same transaction as the status change — both are saved together below.
        var workflowResult = await _workflowService.InitializeWorkflowAsync(
            application.Id,
            application.Type,
            LoanApplicationStatus.CreditReview,
            request.UserId,
            ct);

        if (workflowResult.IsFailure)
            return ApplicationResult.Failure(workflowResult.Error);

        _repository.Update(application);

        // Enqueue credit bureau checks atomically with the submission — same transaction,
        // so if the save fails neither the status change nor the outbox entry is persisted.
        await _outbox.EnqueueAsync(application.Id, request.UserId, ct);

        await _unitOfWork.SaveChangesAsync(ct);

        // Auto-analyze all Pending bank statements after submission is committed.
        // Non-fatal: failures are logged but do not roll back the already-committed submission.
        await AutoAnalyzeStatementsAsync(request.ApplicationId, ct);

        return ApplicationResult.Success();
    }

    private async Task AutoAnalyzeStatementsAsync(Guid applicationId, CancellationToken ct)
    {
        try
        {
            var statements = await _statementRepository.GetByLoanApplicationIdAsync(applicationId, ct);
            var pending = statements.Where(s => s.AnalysisStatus == AnalysisStatus.Pending).ToList();
            if (pending.Count == 0) return;

            _logger.LogInformation("Auto-analyzing {Count} bank statement(s) for application {ApplicationId}", pending.Count, applicationId);

            foreach (var stmt in pending)
            {
                try
                {
                    var analysisResult = await _analyzeStatementHandler.Handle(new AnalyzeStatementCommand(stmt.Id), ct);
                    if (!analysisResult.IsSuccess)
                        _logger.LogWarning("Auto-analysis skipped for statement {StatementId}: {Reason}", stmt.Id, analysisResult.Error);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-analysis failed for statement {StatementId}", stmt.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-analysis fetch failed for application {ApplicationId}", applicationId);
        }
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

public record MoveToLegalReviewCommand(Guid ApplicationId, Guid UserId) : IRequest<ApplicationResult>;

public class MoveToLegalReviewHandler : IRequestHandler<MoveToLegalReviewCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public MoveToLegalReviewHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(MoveToLegalReviewCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.MoveToLegalReview(request.UserId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record SubmitLegalOpinionCommand(Guid ApplicationId, Guid UserId, string? Comment) : IRequest<ApplicationResult>;

public class SubmitLegalOpinionHandler : IRequestHandler<SubmitLegalOpinionCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitLegalOpinionHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(SubmitLegalOpinionCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.SubmitLegalOpinion(request.UserId, request.Comment);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ApproveLegalReviewCommand(Guid ApplicationId, Guid UserId, string? Comment) : IRequest<ApplicationResult>;

public class ApproveLegalReviewHandler : IRequestHandler<ApproveLegalReviewCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveLegalReviewHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ApproveLegalReviewCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ApproveLegalReview(request.UserId, request.Comment);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ReturnFromLegalApprovalCommand(Guid ApplicationId, Guid UserId, string Reason) : IRequest<ApplicationResult>;

public class ReturnFromLegalApprovalHandler : IRequestHandler<ReturnFromLegalApprovalCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnFromLegalApprovalHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ReturnFromLegalApprovalCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ReturnFromLegalApproval(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ReturnFromLegalReviewCommand(Guid ApplicationId, Guid UserId, string Reason) : IRequest<ApplicationResult>;

public class ReturnFromLegalReviewHandler : IRequestHandler<ReturnFromLegalReviewCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnFromLegalReviewHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ReturnFromLegalReviewCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ReturnFromLegalReview(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

// Approve committee decision — sets domain status to CommitteeApproved then immediately FinalApproval
public record ApproveCommitteeCommand(
    Guid ApplicationId,
    Guid UserId,
    decimal? ApprovedAmount,
    int? ApprovedTenorMonths,
    decimal? ApprovedInterestRate
) : IRequest<ApplicationResult>;

public class ApproveCommitteeHandler : IRequestHandler<ApproveCommitteeCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveCommitteeHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ApproveCommitteeCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var amount = request.ApprovedAmount.HasValue
            ? Money.Create(request.ApprovedAmount.Value, application.RequestedAmount.Currency)
            : application.RequestedAmount;
        var tenor = request.ApprovedTenorMonths ?? application.RequestedTenorMonths;
        var rate = request.ApprovedInterestRate ?? application.InterestRatePerAnnum;

        var approveResult = application.ApproveCommittee(request.UserId, amount, tenor, rate);
        if (approveResult.IsFailure)
            return ApplicationResult.Failure(approveResult.Error);

        var moveResult = application.MoveToFinalApproval(request.UserId);
        if (moveResult.IsFailure)
            return ApplicationResult.Failure(moveResult.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

// Reject committee decision — sets domain status to CommitteeRejected
public record RejectCommitteeCommand(
    Guid ApplicationId,
    Guid UserId,
    string Reason
) : IRequest<ApplicationResult>;

public class RejectCommitteeHandler : IRequestHandler<RejectCommitteeCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectCommitteeHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(RejectCommitteeCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.RejectCommittee(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record MoveToSecurityPerfectionCommand(Guid ApplicationId, Guid UserId) : IRequest<ApplicationResult>;

public class MoveToSecurityPerfectionHandler : IRequestHandler<MoveToSecurityPerfectionCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public MoveToSecurityPerfectionHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(MoveToSecurityPerfectionCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.MoveToSecurityPerfection(request.UserId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record SubmitSecurityDocumentsCommand(Guid ApplicationId, Guid UserId, string? Comment) : IRequest<ApplicationResult>;

public class SubmitSecurityDocumentsHandler : IRequestHandler<SubmitSecurityDocumentsCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitSecurityDocumentsHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(SubmitSecurityDocumentsCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.SubmitSecurityDocuments(request.UserId, request.Comment);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ApproveSecurityPerfectionCommand(Guid ApplicationId, Guid UserId, string? Comment) : IRequest<ApplicationResult>;

public class ApproveSecurityPerfectionHandler : IRequestHandler<ApproveSecurityPerfectionCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveSecurityPerfectionHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ApproveSecurityPerfectionCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ApproveSecurityPerfection(request.UserId, request.Comment);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ReturnFromSecurityPerfectionCommand(Guid ApplicationId, Guid UserId, string Reason) : IRequest<ApplicationResult>;

public class ReturnFromSecurityPerfectionHandler : IRequestHandler<ReturnFromSecurityPerfectionCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnFromSecurityPerfectionHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ReturnFromSecurityPerfectionCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ReturnFromSecurityPerfection(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ReturnFromSecurityApprovalCommand(Guid ApplicationId, Guid UserId, string Reason) : IRequest<ApplicationResult>;

public class ReturnFromSecurityApprovalHandler : IRequestHandler<ReturnFromSecurityApprovalCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnFromSecurityApprovalHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ReturnFromSecurityApprovalCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ReturnFromSecurityApproval(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record PrepareDisbursementMemoCommand(Guid ApplicationId, Guid UserId, string? Comment) : IRequest<ApplicationResult>;

public class PrepareDisbursementMemoHandler : IRequestHandler<PrepareDisbursementMemoCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public PrepareDisbursementMemoHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(PrepareDisbursementMemoCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.PrepareDisbursementMemo(request.UserId, request.Comment);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ApproveDisbursementBranchCommand(Guid ApplicationId, Guid UserId, string? Comment) : IRequest<ApplicationResult>;

public class ApproveDisbursementBranchHandler : IRequestHandler<ApproveDisbursementBranchCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveDisbursementBranchHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ApproveDisbursementBranchCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ApproveDisbursementBranch(request.UserId, request.Comment);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ReturnFromDisbursementBranchCommand(Guid ApplicationId, Guid UserId, string Reason) : IRequest<ApplicationResult>;

public class ReturnFromDisbursementBranchHandler : IRequestHandler<ReturnFromDisbursementBranchCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnFromDisbursementBranchHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ReturnFromDisbursementBranchCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ReturnFromDisbursementBranch(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ReturnFromDisbursementPendingCommand(Guid ApplicationId, Guid UserId, string Reason) : IRequest<ApplicationResult>;

public class ReturnFromDisbursementPendingHandler : IRequestHandler<ReturnFromDisbursementPendingCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnFromDisbursementPendingHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ReturnFromDisbursementPendingCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.ReturnFromDisbursementPending(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record ApproveDisbursementHQCommand(Guid ApplicationId, Guid UserId, string CoreBankingLoanId, string? Comment) : IRequest<ApplicationResult>;

public class ApproveDisbursementHQHandler : IRequestHandler<ApproveDisbursementHQCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveDisbursementHQHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ApproveDisbursementHQCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var result = application.RecordDisbursement(request.CoreBankingLoanId, request.UserId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}
