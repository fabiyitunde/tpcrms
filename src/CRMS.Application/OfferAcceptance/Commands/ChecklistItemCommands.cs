using CRMS.Application.Common;
using CRMS.Domain.Constants;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.OfferAcceptance.Commands;

// ---------------------------------------------------------------------------
// Satisfy — LoanOfficer satisfies a non-legal item (with optional evidence doc)
// ---------------------------------------------------------------------------

public record SatisfyChecklistItemCommand(
    Guid LoanApplicationId,
    Guid ChecklistItemId,
    Guid SatisfiedByUserId,
    string SatisfiedByUserRole,
    Guid? EvidenceDocumentId
) : IRequest<ApplicationResult>;

public class SatisfyChecklistItemHandler : IRequestHandler<SatisfyChecklistItemCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SatisfyChecklistItemHandler(ILoanApplicationRepository loanAppRepository, IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(SatisfyChecklistItemCommand request, CancellationToken ct = default)
    {
        if (request.SatisfiedByUserRole is not (Roles.LoanOfficer or Roles.Operations or Roles.SystemAdmin))
            return ApplicationResult.Failure("Only LoanOfficer or Operations may satisfy checklist items");

        var loanApp = await _loanAppRepository.GetByIdWithChecklistAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult.Failure("Loan application not found");

        var item = loanApp.ChecklistItems.FirstOrDefault(i => i.Id == request.ChecklistItemId);
        if (item == null)
            return ApplicationResult.Failure("Checklist item not found");

        var result = item.Satisfy(request.SatisfiedByUserId, request.EvidenceDocumentId);
        if (!result.IsSuccess)
            return ApplicationResult.Failure(result.Error!);

        _loanAppRepository.Update(loanApp);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ---------------------------------------------------------------------------
// Submit for legal review — LoanOfficer uploads document, sends to LegalOfficer
// ---------------------------------------------------------------------------

public record SubmitForLegalReviewCommand(
    Guid LoanApplicationId,
    Guid ChecklistItemId,
    Guid SubmittedByUserId,
    string SubmittedByUserRole,
    Guid EvidenceDocumentId
) : IRequest<ApplicationResult>;

public class SubmitForLegalReviewHandler : IRequestHandler<SubmitForLegalReviewCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitForLegalReviewHandler(ILoanApplicationRepository loanAppRepository, IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(SubmitForLegalReviewCommand request, CancellationToken ct = default)
    {
        if (request.SubmittedByUserRole is not (Roles.LoanOfficer or Roles.Operations or Roles.SystemAdmin))
            return ApplicationResult.Failure("Only LoanOfficer may submit items for legal review");

        var loanApp = await _loanAppRepository.GetByIdWithChecklistAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult.Failure("Loan application not found");

        var item = loanApp.ChecklistItems.FirstOrDefault(i => i.Id == request.ChecklistItemId);
        if (item == null)
            return ApplicationResult.Failure("Checklist item not found");

        var result = item.SubmitForLegalReview(request.SubmittedByUserId, request.EvidenceDocumentId);
        if (!result.IsSuccess)
            return ApplicationResult.Failure(result.Error!);

        _loanAppRepository.Update(loanApp);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ---------------------------------------------------------------------------
// Ratify by Legal — LegalOfficer approves or returns the document
// ---------------------------------------------------------------------------

public record RatifyLegalItemCommand(
    Guid LoanApplicationId,
    Guid ChecklistItemId,
    Guid LegalOfficerUserId,
    bool IsApproved,
    string? RejectionReason
) : IRequest<ApplicationResult>;

public class RatifyLegalItemHandler : IRequestHandler<RatifyLegalItemCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RatifyLegalItemHandler(ILoanApplicationRepository loanAppRepository, IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(RatifyLegalItemCommand request, CancellationToken ct = default)
    {
        var loanApp = await _loanAppRepository.GetByIdWithChecklistAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult.Failure("Loan application not found");

        var item = loanApp.ChecklistItems.FirstOrDefault(i => i.Id == request.ChecklistItemId);
        if (item == null)
            return ApplicationResult.Failure("Checklist item not found");

        var result = request.IsApproved
            ? item.RatifyByLegal(request.LegalOfficerUserId)
            : item.ReturnByLegal(request.LegalOfficerUserId, request.RejectionReason ?? "");

        if (!result.IsSuccess)
            return ApplicationResult.Failure(result.Error!);

        _loanAppRepository.Update(loanApp);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ---------------------------------------------------------------------------
// Propose waiver — LoanOfficer proposes, requires RiskManager ratification
// ---------------------------------------------------------------------------

public record ProposeWaiverCommand(
    Guid LoanApplicationId,
    Guid ChecklistItemId,
    Guid ProposedByUserId,
    string ProposedByUserRole,
    string WaiverReason
) : IRequest<ApplicationResult>;

public class ProposeWaiverHandler : IRequestHandler<ProposeWaiverCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProposeWaiverHandler(ILoanApplicationRepository loanAppRepository, IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ProposeWaiverCommand request, CancellationToken ct = default)
    {
        if (request.ProposedByUserRole is not (Roles.LoanOfficer or Roles.SystemAdmin))
            return ApplicationResult.Failure("Only LoanOfficer may propose waivers");

        var loanApp = await _loanAppRepository.GetByIdWithChecklistAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult.Failure("Loan application not found");

        var item = loanApp.ChecklistItems.FirstOrDefault(i => i.Id == request.ChecklistItemId);
        if (item == null)
            return ApplicationResult.Failure("Checklist item not found");

        var result = item.ProposeWaiver(request.ProposedByUserId, request.WaiverReason);
        if (!result.IsSuccess)
            return ApplicationResult.Failure(result.Error!);

        _loanAppRepository.Update(loanApp);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ---------------------------------------------------------------------------
// Ratify waiver — RiskManager approves or rejects
// ---------------------------------------------------------------------------

public record RatifyWaiverCommand(
    Guid LoanApplicationId,
    Guid ChecklistItemId,
    Guid RiskManagerUserId,
    bool IsApproved,
    string? RejectionReason
) : IRequest<ApplicationResult>;

public class RatifyWaiverHandler : IRequestHandler<RatifyWaiverCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RatifyWaiverHandler(ILoanApplicationRepository loanAppRepository, IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(RatifyWaiverCommand request, CancellationToken ct = default)
    {
        var loanApp = await _loanAppRepository.GetByIdWithChecklistAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult.Failure("Loan application not found");

        var item = loanApp.ChecklistItems.FirstOrDefault(i => i.Id == request.ChecklistItemId);
        if (item == null)
            return ApplicationResult.Failure("Checklist item not found");

        var result = request.IsApproved
            ? item.ApproveWaiver(request.RiskManagerUserId)
            : item.RejectWaiver(request.RiskManagerUserId, request.RejectionReason ?? "");

        if (!result.IsSuccess)
            return ApplicationResult.Failure(result.Error!);

        _loanAppRepository.Update(loanApp);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ---------------------------------------------------------------------------
// Request CS extension — LoanOfficer requests more time on a Subsequent item
// ---------------------------------------------------------------------------

public record RequestCsExtensionCommand(
    Guid LoanApplicationId,
    Guid ChecklistItemId,
    Guid RequestedByUserId,
    string RequestedByUserRole,
    string Reason
) : IRequest<ApplicationResult>;

public class RequestCsExtensionHandler : IRequestHandler<RequestCsExtensionCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RequestCsExtensionHandler(ILoanApplicationRepository loanAppRepository, IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(RequestCsExtensionCommand request, CancellationToken ct = default)
    {
        if (request.RequestedByUserRole is not (Roles.LoanOfficer or Roles.Operations or Roles.SystemAdmin))
            return ApplicationResult.Failure("Only LoanOfficer or Operations may request extensions");

        var loanApp = await _loanAppRepository.GetByIdWithChecklistAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult.Failure("Loan application not found");

        var item = loanApp.ChecklistItems.FirstOrDefault(i => i.Id == request.ChecklistItemId);
        if (item == null)
            return ApplicationResult.Failure("Checklist item not found");

        var result = item.RequestExtension(request.RequestedByUserId, request.Reason);
        if (!result.IsSuccess)
            return ApplicationResult.Failure(result.Error!);

        _loanAppRepository.Update(loanApp);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ---------------------------------------------------------------------------
// Ratify extension — RiskManager approves or rejects CS due-date extension
// ---------------------------------------------------------------------------

public record RatifyExtensionCommand(
    Guid LoanApplicationId,
    Guid ChecklistItemId,
    Guid RiskManagerUserId,
    bool IsApproved,
    int AdditionalDays
) : IRequest<ApplicationResult>;

public class RatifyExtensionHandler : IRequestHandler<RatifyExtensionCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RatifyExtensionHandler(ILoanApplicationRepository loanAppRepository, IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(RatifyExtensionCommand request, CancellationToken ct = default)
    {
        var loanApp = await _loanAppRepository.GetByIdWithChecklistAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult.Failure("Loan application not found");

        var item = loanApp.ChecklistItems.FirstOrDefault(i => i.Id == request.ChecklistItemId);
        if (item == null)
            return ApplicationResult.Failure("Checklist item not found");

        var result = request.IsApproved
            ? item.ApproveExtension(request.RiskManagerUserId, request.AdditionalDays)
            : item.RejectExtension(request.RiskManagerUserId);

        if (!result.IsSuccess)
            return ApplicationResult.Failure(result.Error!);

        _loanAppRepository.Update(loanApp);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
