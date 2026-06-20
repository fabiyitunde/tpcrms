using CRMS.Application.Common;
using CRMS.Application.CreditBureau.Interfaces;
using CRMS.Application.Namp.DTOs;
using CRMS.Application.Namp.Interfaces;
using CRMS.Application.Namp.Queries;
using CRMS.Application.OfferLetter.Interfaces;
using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Application.Namp.Commands;

// ── Stage 1: Submit ────────────────────────────────────────────────────────

public record SubmitNampApplicationCommand(Guid NampApplicationId, Guid UserId)
    : IRequest<ApplicationResult<NampApplicationDto>>;

public class SubmitNampApplicationHandler
    : IRequestHandler<SubmitNampApplicationCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly ICreditCheckOutbox _outbox;
    private readonly IUnitOfWork _uow;

    public SubmitNampApplicationHandler(INampApplicationRepository repo, ICreditCheckOutbox outbox, IUnitOfWork uow)
    {
        _repo = repo;
        _outbox = outbox;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        SubmitNampApplicationCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = app.Submit(request.UserId);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        // Enqueue bureau checks atomically — background service picks this up within 30 seconds.
        // Committed in the same SaveChanges as the status change so no outbox entry is lost.
        await _outbox.EnqueueForNampAsync(app.Id, request.UserId, ct);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

// ── Stage 2: Financial Appraisal Decision ─────────────────────────────────

public record SubmitNampFinancialAppraisalCommand(
    Guid NampApplicationId,
    Guid UserId,
    bool IsApproved,
    string? Note
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class SubmitNampFinancialAppraisalHandler
    : IRequestHandler<SubmitNampFinancialAppraisalCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public SubmitNampFinancialAppraisalHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        SubmitNampFinancialAppraisalCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var report = await _repo.GetFinancialAppraisalReportAsync(request.NampApplicationId, ct);
        if (report is null)
            return ApplicationResult<NampApplicationDto>.Failure("Financial appraisal report must be saved before submitting a decision.");

        var hasRequiredDoc = app.Documents.Any(d =>
            d.Stage == NampDocumentStage.FinancialAppraisal &&
            d.Category == NampDocumentCategory.CreditReport);
        if (!hasRequiredDoc)
            return ApplicationResult<NampApplicationDto>.Failure("At least one Credit Report document must be uploaded before submitting.");

        var result = app.SubmitFinancialAppraisal(request.UserId, request.IsApproved, request.Note);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

// ── Stage 4a: Circulate to Committee ─────────────────────────────────────

public record NampCommitteeMemberInput(Guid UserId, string UserName, string Role, bool IsChairperson = false);

public record CirculateNampToCommitteeCommand(
    Guid NampApplicationId,
    Guid CirculatedByUserId,
    List<NampCommitteeMemberInput> Members,
    int RequiredVotes,
    int MinimumApprovalVotes,
    int DeadlineHours = 72
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class CirculateNampToCommitteeHandler
    : IRequestHandler<CirculateNampToCommitteeCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly ICommitteeReviewRepository _committeeRepo;
    private readonly IUnitOfWork _uow;

    public CirculateNampToCommitteeHandler(
        INampApplicationRepository repo,
        ICommitteeReviewRepository committeeRepo,
        IUnitOfWork uow)
    {
        _repo = repo;
        _committeeRepo = committeeRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        CirculateNampToCommitteeCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var committeeType = app.CommitteeTier switch
        {
            NampCommitteeTier.Branch => CommitteeType.BranchCredit,
            NampCommitteeTier.Zonal => CommitteeType.ZonalCredit,
            NampCommitteeTier.Regional => CommitteeType.RegionalCredit,
            NampCommitteeTier.HeadOffice => CommitteeType.HeadOfficeCredit,
            _ => throw new InvalidOperationException($"Unknown tier: {app.CommitteeTier}")
        };

        // Use Guid.Empty as loanApplicationId — NAMP applications are not LoanApplications
        var review = CommitteeReview.Create(
            loanApplicationId: Guid.Empty,
            applicationNumber: app.ApplicationNumber,
            committeeType: committeeType,
            circulatedByUserId: request.CirculatedByUserId,
            requiredVotes: request.RequiredVotes,
            minimumApprovalVotes: request.MinimumApprovalVotes,
            deadlineHours: request.DeadlineHours);

        if (review.IsFailure)
            return ApplicationResult<NampApplicationDto>.Failure(review.Error);

        var committeeReview = review.Value;
        foreach (var m in request.Members)
        {
            var addResult = committeeReview.AddMember(m.UserId, m.UserName, m.Role, m.IsChairperson);
            if (addResult.IsFailure)
                return ApplicationResult<NampApplicationDto>.Failure($"Could not add member {m.UserName}: {addResult.Error}");
        }

        // Auto-start voting immediately upon circulation
        var startResult = committeeReview.StartVoting();
        if (startResult.IsFailure)
            return ApplicationResult<NampApplicationDto>.Failure(startResult.Error);

        await _committeeRepo.AddAsync(committeeReview, ct);

        var circulateResult = app.CirculateToCommittee(committeeReview.Id, request.CirculatedByUserId);
        if (circulateResult.IsFailure)
            return ApplicationResult<NampApplicationDto>.Failure(circulateResult.Error);

        app.SetAuditInfo(request.CirculatedByUserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

// ── Stage 4b: Cast Individual Committee Vote ───────────────────────────────

public record CastNampCommitteeVoteCommand(
    Guid CommitteeReviewId,
    Guid UserId,
    string Vote,
    string? Comment
) : IRequest<ApplicationResult<NampCommitteeReviewDto>>;

public class CastNampCommitteeVoteHandler
    : IRequestHandler<CastNampCommitteeVoteCommand, ApplicationResult<NampCommitteeReviewDto>>
{
    private readonly ICommitteeReviewRepository _committeeRepo;
    private readonly INampApplicationRepository _nampRepo;
    private readonly IUnitOfWork _uow;

    public CastNampCommitteeVoteHandler(
        ICommitteeReviewRepository committeeRepo,
        INampApplicationRepository nampRepo,
        IUnitOfWork uow)
    {
        _committeeRepo = committeeRepo;
        _nampRepo = nampRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampCommitteeReviewDto>> Handle(
        CastNampCommitteeVoteCommand request, CancellationToken ct = default)
    {
        var review = await _committeeRepo.GetByIdAsync(request.CommitteeReviewId, ct);
        if (review is null) return ApplicationResult<NampCommitteeReviewDto>.Failure("Committee review not found.");

        if (!Enum.TryParse<CommitteeVote>(request.Vote, ignoreCase: true, out var vote))
            return ApplicationResult<NampCommitteeReviewDto>.Failure($"Invalid vote value: '{request.Vote}'.");

        var result = review.CastVote(request.UserId, vote, request.Comment);
        if (result.IsFailure) return ApplicationResult<NampCommitteeReviewDto>.Failure(result.Error);

        // Auto-resolve outcome once every member has voted
        if (review.Status == CommitteeReviewStatus.VotingComplete)
        {
            var app = await _nampRepo.GetByCommitteeReviewIdAsync(review.Id, ct);
            if (app is not null)
            {
                app.RecordCommitteeOutcome(request.UserId, review.HasMajorityApproval, note: null);
                app.SetAuditInfo(request.UserId.ToString());
            }
        }

        _committeeRepo.Update(review);
        await _uow.SaveChangesAsync(ct);

        var dto = new NampCommitteeReviewDto(
            review.Id, review.CommitteeType.ToString(), review.Status.ToString(),
            review.CirculatedAt, review.DeadlineAt, review.RequiredVotes, review.MinimumApprovalVotes,
            review.ApprovalVotes, review.RejectionVotes, review.AbstainVotes, review.PendingVotes,
            review.HasQuorum, review.HasMajorityApproval, review.IsOverdue,
            review.Members.Select(m => new NampCommitteeMemberViewDto(
                m.UserId, m.UserName, m.Role, m.IsChairperson, m.AssignedAt,
                m.Vote?.ToString(), m.VotedAt, m.VoteComment
            )).ToList()
        );
        return ApplicationResult<NampCommitteeReviewDto>.Success(dto);
    }
}

// ── Stage 5: Ratification ─────────────────────────────────────────────────

public record RatifyNampDecisionCommand(
    Guid NampApplicationId,
    Guid UserId,
    string BankName,
    string BranchName,
    string? Note = null
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class RatifyNampDecisionHandler
    : IRequestHandler<RatifyNampDecisionCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly ILoanProductRepository _productRepo;
    private readonly IFineractDirectService _fineractService;
    private readonly INampOfferLetterPdfGenerator _pdfGenerator;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public RatifyNampDecisionHandler(
        INampApplicationRepository repo,
        ILoanProductRepository productRepo,
        IFineractDirectService fineractService,
        INampOfferLetterPdfGenerator pdfGenerator,
        IFileStorageService fileStorage,
        IUnitOfWork uow)
    {
        _repo = repo;
        _productRepo = productRepo;
        _fineractService = fineractService;
        _pdfGenerator = pdfGenerator;
        _fileStorage = fileStorage;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        RatifyNampDecisionCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        if (app.LoanAmount is null or <= 0)
            return ApplicationResult<NampApplicationDto>.Failure("Loan amount must be set before ratification.");

        // ── Resolve product, interest rate and tenor from Fineract ────────────
        // Fineract is the source of truth for both values. Always fetch the live
        // product so we use current figures rather than values cached at recall.
        var fineractProductId = app.FineractProductId ?? 0;
        if (fineractProductId <= 0)
        {
            var product = await _productRepo.GetByIdAsync(app.LoanProductId, ct);
            fineractProductId = product?.FineractProductId ?? 0;
        }

        if (fineractProductId <= 0)
            return ApplicationResult<NampApplicationDto>.Failure(
                "No Fineract product linked to this application. Cannot resolve interest rate or tenor.");

        var productsResult = await _fineractService.GetLoanProductsAsync(activeOnly: false, ct);
        if (!productsResult.IsSuccess)
            return ApplicationResult<NampApplicationDto>.Failure(
                $"Could not fetch Fineract product catalogue: {productsResult.Error}");

        var fineractProduct = productsResult.Value.FirstOrDefault(p => p.Id == fineractProductId);
        if (fineractProduct is null)
            return ApplicationResult<NampApplicationDto>.Failure(
                $"Fineract product {fineractProductId} not found in catalogue.");

        var interestRate = fineractProduct.AnnualInterestRate;
        var tenorMonths  = fineractProduct.DefaultNumberOfRepayments;

        // Lock both onto the application so downstream steps (offer letter, deployment) read from the app
        app.SetApprovedInterestRate(interestRate);
        app.SetResolvedTenor(tenorMonths);

        // ── Calculate repayment schedule ───────────────────────────────────────
        var scheduleRequest = new ScheduleCalculationRequest(
            ProductId: fineractProductId,
            Principal: app.LoanAmount.Value,
            NumberOfRepayments: tenorMonths,
            RepaymentEvery: 1,
            RepaymentFrequencyType: 2,           // Months
            InterestRatePerPeriod: interestRate,
            InterestRateFrequencyType: 3,        // Per Year
            AmortizationType: 1,                 // Equal Installments (EMI)
            InterestType: 0,                     // Declining Balance
            InterestCalculationPeriodType: 1,    // Same as Repayment Period
            ExpectedDisbursementDate: DateTime.Today.AddDays(14)
        );

        var scheduleResult = await _fineractService.CalculateRepaymentScheduleAsync(scheduleRequest, ct);
        if (scheduleResult.IsFailure)
            return ApplicationResult<NampApplicationDto>.Failure(
                $"Failed to calculate repayment schedule: {scheduleResult.Error}");

        var schedule = scheduleResult.Value;
        var scheduleSource = fineractProductId > 0 ? "Fineract" : "InHouse";
        var monthlyInstallment = schedule.Installments.Any()
            ? Math.Round(schedule.Installments.Average(i => i.TotalDue), 2)
            : 0;

        // ── Build and generate offer letter PDF ────────────────────────────────
        var offerData = new NampOfferLetterData(
            ApplicationNumber: app.ApplicationNumber,
            ApplicationReference: app.ApplicationReference,
            GeneratedDate: DateTime.UtcNow,
            ApplicantName: app.ApplicantName,
            BoaAccountNumber: app.BoaAccountNumber,
            ApplicantCategory: app.ApplicantCategory.ToString(),
            EquipmentDescription: app.EquipmentDescription,
            EquipmentValue: app.EquipmentValue,
            LoanAmount: app.LoanAmount,
            EquityAmount: app.EquityAmount,
            EquityPercent: app.EquityPercent,
            TenorMonths: tenorMonths,
            InterestRatePerAnnum: interestRate,
            LoanPurpose: app.LoanPurpose,
            CommitteeConditions: app.CommitteeDecisionNote,
            BankName: request.BankName,
            BranchName: request.BranchName,
            RepaymentSchedule: schedule.Installments.Select(i => new ScheduleInstallmentData(
                InstallmentNumber: i.PeriodNumber,
                DueDate: i.DueDate,
                Principal: i.PrincipalDue,
                Interest: i.InterestDue,
                TotalPayment: i.TotalDue,
                OutstandingBalance: i.OutstandingBalance
            )).ToList(),
            TotalPrincipal: schedule.TotalPrincipal,
            TotalInterest: schedule.TotalInterest,
            TotalRepayment: schedule.TotalRepayment,
            MonthlyInstallment: monthlyInstallment,
            ScheduleSource: scheduleSource
        );

        var pdfBytes = await _pdfGenerator.GenerateAsync(offerData, ct);
        var fileName = $"NAMP_OfferLetter_{app.ApplicationNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        var storagePath = await _fileStorage.UploadAsync(
            "namp-offerletters",
            $"{app.ApplicationNumber}/{fileName}",
            pdfBytes,
            "application/pdf",
            ct);

        var result = app.Ratify(request.UserId, storagePath, request.Note);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

public record DeclineNampRatificationCommand(
    Guid NampApplicationId,
    Guid UserId,
    string? Note
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class DeclineNampRatificationHandler
    : IRequestHandler<DeclineNampRatificationCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public DeclineNampRatificationHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        DeclineNampRatificationCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = app.DeclineRatification(request.UserId, request.Note);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

// ── Stage 5b: Offer ───────────────────────────────────────────────────────

public record RecordNampOfferAcceptanceCommand(Guid NampApplicationId, Guid UserId)
    : IRequest<ApplicationResult<NampApplicationDto>>;

public class RecordNampOfferAcceptanceHandler
    : IRequestHandler<RecordNampOfferAcceptanceCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public RecordNampOfferAcceptanceHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        RecordNampOfferAcceptanceCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = app.RecordOfferAcceptance(request.UserId);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

public record LapseNampOfferCommand(Guid NampApplicationId, Guid UserId)
    : IRequest<ApplicationResult<NampApplicationDto>>;

public class LapseNampOfferHandler
    : IRequestHandler<LapseNampOfferCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public LapseNampOfferHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        LapseNampOfferCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = app.LapseOffer(request.UserId);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

// ── Stage 6: Pre-Deployment Verification ──────────────────────────────────

public record BeginNampPreDeploymentVerificationCommand(Guid NampApplicationId, Guid UserId)
    : IRequest<ApplicationResult<NampApplicationDto>>;

public class BeginNampPreDeploymentVerificationHandler
    : IRequestHandler<BeginNampPreDeploymentVerificationCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly INampPreDeploymentChecklistTemplateRepository _templateRepo;
    private readonly IUnitOfWork _uow;

    public BeginNampPreDeploymentVerificationHandler(
        INampApplicationRepository repo,
        INampPreDeploymentChecklistTemplateRepository templateRepo,
        IUnitOfWork uow)
    {
        _repo = repo;
        _templateRepo = templateRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        BeginNampPreDeploymentVerificationCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = app.BeginPreDeploymentVerification(request.UserId);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        // Seed checklist items from active templates (skip if already seeded)
        if (!app.PreDeploymentChecklist.Any())
        {
            var templates = await _templateRepo.GetAllAsync(ct);
            var items = templates
                .Where(t => t.IsActive)
                .OrderBy(t => t.SortOrder)
                .Select(t => NampPreDeploymentChecklistItem.FromTemplate(app.Id, t))
                .ToList();

            if (items.Count > 0)
                await _repo.AddPreDeploymentChecklistItemsAsync(items, ct);
        }

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

public record CompleteNampPreDeploymentVerificationCommand(
    Guid NampApplicationId,
    Guid UserId,
    string? Note
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class CompleteNampPreDeploymentVerificationHandler
    : IRequestHandler<CompleteNampPreDeploymentVerificationCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public CompleteNampPreDeploymentVerificationHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        CompleteNampPreDeploymentVerificationCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = app.CompletePreDeploymentVerification(request.UserId, request.Note);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

// ── Stage 6: Deployment ───────────────────────────────────────────────────

public record ConfirmNampDeploymentCommand(
    Guid NampApplicationId,
    Guid UserId,
    bool GpsActivated,
    string? Note
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class ConfirmNampDeploymentHandler
    : IRequestHandler<ConfirmNampDeploymentCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly ILoanProductRepository _productRepo;
    private readonly IFineractDirectService _fineractService;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ConfirmNampDeploymentHandler> _logger;

    public ConfirmNampDeploymentHandler(
        INampApplicationRepository repo,
        ILoanProductRepository productRepo,
        IFineractDirectService fineractService,
        IUnitOfWork uow,
        ILogger<ConfirmNampDeploymentHandler> logger)
    {
        _repo           = repo;
        _productRepo    = productRepo;
        _fineractService = fineractService;
        _uow            = uow;
        _logger         = logger;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        ConfirmNampDeploymentCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        // ── Fineract loan booking ─────────────────────────────────────────────
        if (app.FineractClientId is null)
        {
            _logger.LogWarning("NAMP deployment: FineractClientId not set on application {Id} — loan will not be booked in Fineract.", app.Id);
        }
        else
        {
            // Use FineractProductId stored on the application at recall. Fall back to a product repo
            // lookup only for applications recalled before this feature was added.
            var fineractProductId = app.FineractProductId ?? 0;
            if (fineractProductId <= 0)
            {
                var product = await _productRepo.GetByIdAsync(app.LoanProductId, ct);
                fineractProductId = product?.FineractProductId ?? 0;
            }

            if (fineractProductId <= 0)
            {
                _logger.LogWarning("NAMP deployment: no FineractProductId on application {Id} or its LoanProduct — loan will not be booked.", app.Id);
            }
            else if (app.LoanAmount is null or <= 0)
            {
                _logger.LogWarning("NAMP deployment: LoanAmount is not set on application {Id} — loan will not be booked.", app.Id);
            }
            else
            {
                if (app.ApprovedInterestRate is null)
                    return ApplicationResult<NampApplicationDto>.Failure(
                        "Approved interest rate is not set — ratification must complete before deployment.");
                if (app.RequestedTenorMonths is null or <= 0)
                    return ApplicationResult<NampApplicationDto>.Failure(
                        "Loan tenor is not set — ratification must complete before deployment.");

                var interestRate = app.ApprovedInterestRate.Value;
                var tenorMonths  = app.RequestedTenorMonths.Value;

                var bookingRequest = new FineractLoanBookingRequest(
                    ClientId: app.FineractClientId.Value,
                    ProductId: fineractProductId,
                    Principal: app.LoanAmount.Value,
                    TenorMonths: tenorMonths,
                    InterestRatePerAnnum: interestRate,
                    ValueDate: DateTime.UtcNow,
                    RepaymentAccountNumber: app.BoaAccountNumber,
                    DisburseToSavings: false,                  // NAMP equipment loans disburse to the vendor, not the applicant's savings
                    CreateRepaymentStandingInstruction: true   // ...but repayments are auto-debited from the applicant's BOA savings via a standing instruction
                );

                var bookingResult = await _fineractService.BookApprovedLoanAsync(bookingRequest, ct);
                if (bookingResult.IsSuccess)
                {
                    app.SetFineractLoanResult(bookingResult.Value.LoanId, bookingResult.Value.LoanAccountNumber);
                    _logger.LogInformation("NAMP deployment: Fineract loan booked — LoanId={LoanId}, Account={Account}, Disbursed={Disbursed}",
                        bookingResult.Value.LoanId, bookingResult.Value.LoanAccountNumber, bookingResult.Value.Disbursed);
                }
                else
                {
                    _logger.LogError("NAMP deployment: Fineract loan booking failed for application {Id}: {Error}",
                        app.Id, bookingResult.Error);
                    return ApplicationResult<NampApplicationDto>.Failure(
                        $"Fineract loan booking failed: {bookingResult.Error}");
                }
            }
        }

        var result = app.ConfirmDeployment(request.UserId, request.GpsActivated, request.Note);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}
