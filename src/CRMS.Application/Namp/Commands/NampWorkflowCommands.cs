using CRMS.Application.Common;
using CRMS.Application.CreditBureau.Interfaces;
using CRMS.Application.Namp.DTOs;
using CRMS.Application.Namp.Queries;
using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

// ── Stage 1: Submit ────────────────────────────────────────────────────────

public record SubmitNampApplicationCommand(Guid NampApplicationId, Guid UserId)
    : IRequest<ApplicationResult<NampApplicationDto>>;

public class SubmitNampApplicationHandler
    : IRequestHandler<SubmitNampApplicationCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public SubmitNampApplicationHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        SubmitNampApplicationCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = app.Submit(request.UserId);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

// ── Stage 2: Technical Appraisal ──────────────────────────────────────────

public record SubmitNampTechnicalAppraisalCommand(
    Guid NampApplicationId,
    Guid UserId,
    bool IsApproved,
    string? Note
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class SubmitNampTechnicalAppraisalHandler
    : IRequestHandler<SubmitNampTechnicalAppraisalCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly ICreditCheckOutbox _creditCheckOutbox;
    private readonly IUnitOfWork _uow;

    public SubmitNampTechnicalAppraisalHandler(
        INampApplicationRepository repo,
        ICreditCheckOutbox creditCheckOutbox,
        IUnitOfWork uow)
    {
        _repo = repo;
        _creditCheckOutbox = creditCheckOutbox;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        SubmitNampTechnicalAppraisalCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var report = await _repo.GetTechnicalAppraisalReportAsync(request.NampApplicationId, ct);
        if (report is null)
            return ApplicationResult<NampApplicationDto>.Failure("Technical appraisal report must be saved before submitting a decision.");

        var hasRequiredDoc = app.Documents.Any(d =>
            d.Stage == NampDocumentStage.TechnicalAppraisal &&
            d.Category == NampDocumentCategory.TechnicalReport);
        if (!hasRequiredDoc)
            return ApplicationResult<NampApplicationDto>.Failure("At least one Technical Report document must be uploaded before submitting.");

        var result = app.SubmitTechnicalAppraisal(request.UserId, request.IsApproved, request.Note);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        app.SetAuditInfo(request.UserId.ToString());

        if (request.IsApproved)
        {
            await _creditCheckOutbox.EnqueueForNampAsync(request.NampApplicationId, request.UserId, ct);
        }

        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

// ── Stage 3: Financial Appraisal ──────────────────────────────────────────

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

        await _committeeRepo.AddAsync(committeeReview, ct);

        var circulateResult = app.CirculateToCommittee(committeeReview.Id, request.CirculatedByUserId);
        if (circulateResult.IsFailure)
            return ApplicationResult<NampApplicationDto>.Failure(circulateResult.Error);

        app.SetAuditInfo(request.CirculatedByUserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

// ── Stage 4b: Record Committee Outcome ────────────────────────────────────

public record RecordNampCommitteeOutcomeCommand(
    Guid NampApplicationId,
    Guid UserId,
    bool IsApproved,
    string? Note
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class RecordNampCommitteeOutcomeHandler
    : IRequestHandler<RecordNampCommitteeOutcomeCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public RecordNampCommitteeOutcomeHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        RecordNampCommitteeOutcomeCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = app.RecordCommitteeOutcome(request.UserId, request.IsApproved, request.Note);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

// ── Stage 5: Ratification ─────────────────────────────────────────────────

public record RatifyNampDecisionCommand(
    Guid NampApplicationId,
    Guid UserId,
    string? OfferLetterStoragePath = null
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class RatifyNampDecisionHandler
    : IRequestHandler<RatifyNampDecisionCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public RatifyNampDecisionHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        RatifyNampDecisionCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = app.Ratify(request.UserId, request.OfferLetterStoragePath);
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
    private readonly IUnitOfWork _uow;

    public BeginNampPreDeploymentVerificationHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        BeginNampPreDeploymentVerificationCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = app.BeginPreDeploymentVerification(request.UserId);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

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

// ── Stage 7: Training ─────────────────────────────────────────────────────

public record CompleteNampTrainingCommand(Guid NampApplicationId, Guid UserId)
    : IRequest<ApplicationResult<NampApplicationDto>>;

public class CompleteNampTrainingHandler
    : IRequestHandler<CompleteNampTrainingCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public CompleteNampTrainingHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        CompleteNampTrainingCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = app.CompleteTraining(request.UserId);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}

// ── Stage 8: Deployment ───────────────────────────────────────────────────

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
    private readonly IUnitOfWork _uow;

    public ConfirmNampDeploymentHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        ConfirmNampDeploymentCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = app.ConfirmDeployment(request.UserId, request.GpsActivated, request.Note);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));
    }
}
