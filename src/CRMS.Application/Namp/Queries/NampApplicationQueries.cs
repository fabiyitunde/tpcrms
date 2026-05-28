using CRMS.Application.Common;
using CRMS.Application.Namp.Commands;
using CRMS.Application.Namp.DTOs;
using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CM = CRMS.Domain.Aggregates.Committee;

namespace CRMS.Application.Namp.Queries;

public record GetNampApplicationByIdQuery(Guid Id)
    : IRequest<ApplicationResult<NampApplicationDto>>;

public class GetNampApplicationByIdHandler
    : IRequestHandler<GetNampApplicationByIdQuery, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IBureauReportRepository _bureauRepo;

    public GetNampApplicationByIdHandler(INampApplicationRepository repo, IBureauReportRepository bureauRepo)
    {
        _repo = repo;
        _bureauRepo = bureauRepo;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        GetNampApplicationByIdQuery request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.Id, ct);
        if (app is null)
            return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var techReport = await _repo.GetTechnicalAppraisalReportAsync(request.Id, ct);
        var finReport = await _repo.GetFinancialAppraisalReportAsync(request.Id, ct);
        var bureauReports = await _bureauRepo.GetByNampApplicationIdAsync(request.Id, ct);

        return ApplicationResult<NampApplicationDto>.Success(MapToDto(app, techReport, finReport, bureauReports));
    }

    internal static NampApplicationDto MapToDto(
        NampApplication app,
        NampTechnicalAppraisalReport? techReport = null,
        NampFinancialAppraisalReport? finReport = null,
        IReadOnlyList<BureauReport>? bureauReports = null) => new(
        app.Id,
        app.ApplicationNumber,
        app.ApplicationReference,
        app.Status.ToString(),
        app.LoanProductId,
        app.ApplicantName,
        app.BoaAccountNumber,
        app.ApplicantPhone,
        app.ApplicantEmail,
        app.ApplicantCategory.ToString(),
        app.EquipmentDescription,
        app.EquipmentValue,
        app.EquipmentCartJson,
        app.BranchId,
        app.OfficeId,
        app.LocationId,
        app.CommitteeTier.ToString(),
        app.RecalledByUserId,
        app.RecalledAt,
        app.SubmittedAt,
        // Extended applicant info
        app.Nin,
        app.Bvn,
        app.BoaAccountName,
        app.LoanPurpose,
        app.IndustrySector,
        app.RequestedTenorMonths,
        app.DateOfBirth,
        app.StateOfResidence,
        app.LocalGovernmentArea,
        app.CompanyName,
        app.RcNumber,
        // Individual credit profile
        app.Occupation,
        app.EmployerName,
        app.EmploymentStatus,
        app.YearsOfExperience,
        app.NumberOfDependants,
        app.EstimatedMonthlyIncome,
        app.OtherIncomeSource,
        app.OtherMonthlyIncome,
        app.MonthlyLivingExpenses,
        app.OwnedAssetsDescription,
        app.EstimatedNetWorth,
        app.ExistingLoanObligations,
        // Equity & loan amounts
        app.EquityPercent,
        app.CartTotal,
        app.EquityAmount,
        app.LoanAmount,
        // Workflow stages
        app.TechnicalAppraisalByUserId,
        app.TechnicalAppraisalAt,
        app.TechnicalAppraisalNote,
        app.FinancialAppraisalByUserId,
        app.FinancialAppraisalAt,
        app.FinancialAppraisalNote,
        app.CurrentCommitteeReviewId,
        app.CommitteeDecisionAt,
        app.CommitteeDecisionNote,
        app.RatifiedByUserId,
        app.RatifiedAt,
        app.OfferLetterStoragePath,
        app.OfferGeneratedAt,
        app.OfferAcceptedAt,
        app.OfferLapsedAt,
        app.PreDeploymentVerifiedByUserId,
        app.PreDeploymentVerifiedAt,
        app.PreDeploymentNote,
        app.TrainingCompletedByUserId,
        app.TrainingCompletedAt,
        app.DeployedByUserId,
        app.DeployedAt,
        app.GpsActivated,
        app.DeploymentNote,
        app.FineractClientId,
        app.ApprovedInterestRate,
        app.FineractLoanId,
        app.FineractLoanAccountNumber,
        app.CreatedAt,
        app.ModifiedAt,
        app.Documents.Select(d => new NampDocumentDto(
            d.Id,
            d.Stage.ToString(),
            d.Category.ToString(),
            d.FileName,
            d.ContentType,
            d.FileSize,
            d.StoragePath,
            d.Description,
            d.UploadedAt,
            d.UploadedByUserId
        )).ToList(),
        app.StatusHistory.Select(h => new NampStatusHistoryDto(
            h.Id,
            h.Status.ToString(),
            h.ChangedAt,
            h.ChangedByUserId,
            h.Note
        )).ToList(),
        app.Guarantors.Select(g => new NampGuarantorDto(
            g.Id, g.FullName, g.Bvn, g.Nin, g.DateOfBirth, g.Gender,
            g.CompanyName, g.CompanyRegistrationNumber, g.Email, g.Phone, g.Address,
            g.GuarantorType, g.GuaranteeType, g.RelationshipToApplicant,
            g.IsDirector, g.IsShareholder, g.ShareholdingPercentage,
            g.DeclaredNetWorth, g.Occupation, g.EmployerName, g.MonthlyIncome,
            g.GuaranteeLimit, g.IsUnlimited
        )).ToList(),
        app.Collaterals.Select(c => new NampCollateralDto(
            c.Id, c.CollateralType, c.Description, c.AssetIdentifier,
            c.Location, c.OwnerName, c.OwnershipType,
            c.MarketValue, c.ForcedSaleValue,
            c.LienType, c.LienReference, c.LienRegistrationAuthority,
            c.IsInsured, c.InsurancePolicyNumber, c.InsuranceCompany,
            c.InsuredValue, c.InsuranceExpiryDate
        )).ToList(),
        app.FinancialStatements.Select(f => new NampFinancialStatementDto(
            f.Id, f.FinancialYear, f.YearEndDate, f.FinancialYearType, f.Currency,
            f.Revenue, f.GrossProfit, f.Ebitda, f.NetProfit,
            f.TotalAssets, f.TotalLiabilities, f.TotalEquity,
            f.NetCashFromOperating, f.AuditorName, f.AuditorFirm, f.AuditOpinion
        )).ToList(),
        techReport != null ? SaveNampTechnicalAppraisalReportHandler.MapToDto(techReport) : null,
        finReport != null ? SaveNampFinancialAppraisalReportHandler.MapToDto(finReport) : null,
        (bureauReports ?? []).Select(r => new NampBureauReportDto(
            r.Id,
            r.SubjectName,
            r.PartyType ?? "NampApplicant",
            r.BVN ?? "",
            r.Status.ToString(),
            r.CreditScore,
            r.ScoreGrade,
            r.DelinquentFacilities,
            r.TotalOutstandingBalance,
            r.TotalOverdue,
            r.FraudRecommendation,
            r.ErrorMessage,
            r.CreatedAt
        )).ToList(),
        app.PreDeploymentChecklist.Select(i => new NampPreDeploymentChecklistItemDto(
            i.Id,
            i.TemplateItemId,
            i.Title,
            i.Description,
            i.RequiresDocumentUpload,
            i.DocumentCategory?.ToString(),
            i.IsMandatory,
            i.SortOrder,
            i.IsConfirmed,
            i.ConfirmedByUserId,
            i.ConfirmedAt,
            i.Notes
        )).ToList()
    );
}

public record GetNampApplicationsByStatusQuery(string Status, Guid? BranchId = null)
    : IRequest<ApplicationResult<List<NampApplicationSummaryDto>>>;

public record GetNampApplicationsByStatusAndTierQuery(string Status, string Tier, Guid? BranchId = null)
    : IRequest<ApplicationResult<List<NampApplicationSummaryDto>>>;

public class GetNampApplicationsByStatusHandler
    : IRequestHandler<GetNampApplicationsByStatusQuery, ApplicationResult<List<NampApplicationSummaryDto>>>
{
    private readonly INampApplicationRepository _repo;

    public GetNampApplicationsByStatusHandler(INampApplicationRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<NampApplicationSummaryDto>>> Handle(
        GetNampApplicationsByStatusQuery request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<NampApplicationStatus>(request.Status, ignoreCase: true, out var status))
            return ApplicationResult<List<NampApplicationSummaryDto>>.Failure($"Unknown status: '{request.Status}'.");

        var apps = await _repo.GetByStatusAsync(status, request.BranchId, ct);
        var dtos = apps.Select(a => new NampApplicationSummaryDto(
            a.Id,
            a.ApplicationNumber,
            a.ApplicationReference,
            a.Status.ToString(),
            a.ApplicantName,
            a.BoaAccountNumber,
            a.ApplicantCategory.ToString(),
            a.EquipmentValue,
            a.CommitteeTier.ToString(),
            a.BranchId,
            a.CreatedAt,
            a.SubmittedAt,
            a.CurrentCommitteeReviewId
        )).ToList();

        return ApplicationResult<List<NampApplicationSummaryDto>>.Success(dtos);
    }
}

public class GetNampApplicationsByStatusAndTierHandler
    : IRequestHandler<GetNampApplicationsByStatusAndTierQuery, ApplicationResult<List<NampApplicationSummaryDto>>>
{
    private readonly INampApplicationRepository _repo;

    public GetNampApplicationsByStatusAndTierHandler(INampApplicationRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<NampApplicationSummaryDto>>> Handle(
        GetNampApplicationsByStatusAndTierQuery request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<NampApplicationStatus>(request.Status, ignoreCase: true, out var status))
            return ApplicationResult<List<NampApplicationSummaryDto>>.Failure($"Unknown status: '{request.Status}'.");

        if (!Enum.TryParse<NampCommitteeTier>(request.Tier, ignoreCase: true, out var tier))
            return ApplicationResult<List<NampApplicationSummaryDto>>.Failure($"Unknown tier: '{request.Tier}'.");

        var apps = await _repo.GetByStatusAndTierAsync(status, tier, request.BranchId, ct);
        var dtos = apps.Select(a => new NampApplicationSummaryDto(
            a.Id,
            a.ApplicationNumber,
            a.ApplicationReference,
            a.Status.ToString(),
            a.ApplicantName,
            a.BoaAccountNumber,
            a.ApplicantCategory.ToString(),
            a.EquipmentValue,
            a.CommitteeTier.ToString(),
            a.BranchId,
            a.CreatedAt,
            a.SubmittedAt,
            a.CurrentCommitteeReviewId
        )).ToList();

        return ApplicationResult<List<NampApplicationSummaryDto>>.Success(dtos);
    }
}

public record GetNampApplicationsByCommitteeMembershipQuery(Guid UserId)
    : IRequest<ApplicationResult<List<NampApplicationSummaryDto>>>;

public class GetNampApplicationsByCommitteeMembershipHandler
    : IRequestHandler<GetNampApplicationsByCommitteeMembershipQuery, ApplicationResult<List<NampApplicationSummaryDto>>>
{
    private readonly INampApplicationRepository _repo;

    public GetNampApplicationsByCommitteeMembershipHandler(INampApplicationRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<NampApplicationSummaryDto>>> Handle(
        GetNampApplicationsByCommitteeMembershipQuery request, CancellationToken ct = default)
    {
        var apps = await _repo.GetByCommitteeMembershipAsync(request.UserId, ct);
        var dtos = apps.Select(a => new NampApplicationSummaryDto(
            a.Id,
            a.ApplicationNumber,
            a.ApplicationReference,
            a.Status.ToString(),
            a.ApplicantName,
            a.BoaAccountNumber,
            a.ApplicantCategory.ToString(),
            a.EquipmentValue,
            a.CommitteeTier.ToString(),
            a.BranchId,
            a.CreatedAt,
            a.SubmittedAt,
            a.CurrentCommitteeReviewId
        )).ToList();

        return ApplicationResult<List<NampApplicationSummaryDto>>.Success(dtos);
    }
}

public record GetNampApplicationsByParticipationQuery(Guid UserId)
    : IRequest<ApplicationResult<List<NampApplicationSummaryDto>>>;

public class GetNampApplicationsByParticipationHandler
    : IRequestHandler<GetNampApplicationsByParticipationQuery, ApplicationResult<List<NampApplicationSummaryDto>>>
{
    private readonly INampApplicationRepository _repo;

    public GetNampApplicationsByParticipationHandler(INampApplicationRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<NampApplicationSummaryDto>>> Handle(
        GetNampApplicationsByParticipationQuery request, CancellationToken ct = default)
    {
        var apps = await _repo.GetByParticipationAsync(request.UserId, ct);
        var dtos = apps.Select(a => new NampApplicationSummaryDto(
            a.Id,
            a.ApplicationNumber,
            a.ApplicationReference,
            a.Status.ToString(),
            a.ApplicantName,
            a.BoaAccountNumber,
            a.ApplicantCategory.ToString(),
            a.EquipmentValue,
            a.CommitteeTier.ToString(),
            a.BranchId,
            a.CreatedAt,
            a.SubmittedAt,
            a.CurrentCommitteeReviewId
        )).ToList();

        return ApplicationResult<List<NampApplicationSummaryDto>>.Success(dtos);
    }
}

public record GetNampCommitteeReviewQuery(Guid CommitteeReviewId)
    : IRequest<ApplicationResult<NampCommitteeReviewDto>>;

public class GetNampCommitteeReviewHandler
    : IRequestHandler<GetNampCommitteeReviewQuery, ApplicationResult<NampCommitteeReviewDto>>
{
    private readonly ICommitteeReviewRepository _committeeRepo;

    public GetNampCommitteeReviewHandler(ICommitteeReviewRepository committeeRepo) => _committeeRepo = committeeRepo;

    public async Task<ApplicationResult<NampCommitteeReviewDto>> Handle(
        GetNampCommitteeReviewQuery request, CancellationToken ct = default)
    {
        var review = await _committeeRepo.GetByIdAsync(request.CommitteeReviewId, ct);
        if (review is null)
            return ApplicationResult<NampCommitteeReviewDto>.Failure("Committee review not found.");

        var dto = new NampCommitteeReviewDto(
            review.Id,
            review.CommitteeType.ToString(),
            review.Status.ToString(),
            review.CirculatedAt,
            review.DeadlineAt,
            review.RequiredVotes,
            review.MinimumApprovalVotes,
            review.ApprovalVotes,
            review.RejectionVotes,
            review.AbstainVotes,
            review.PendingVotes,
            review.HasQuorum,
            review.HasMajorityApproval,
            review.IsOverdue,
            review.Members.Select(m => new NampCommitteeMemberViewDto(
                m.UserId,
                m.UserName,
                m.Role,
                m.IsChairperson,
                m.AssignedAt,
                m.Vote?.ToString(),
                m.VotedAt,
                m.VoteComment
            )).ToList()
        );

        return ApplicationResult<NampCommitteeReviewDto>.Success(dto);
    }
}

public record GetNampPreDeploymentChecklistTemplatesQuery
    : IRequest<ApplicationResult<List<NampPreDeploymentChecklistTemplateDto>>>;

public class GetNampPreDeploymentChecklistTemplatesHandler
    : IRequestHandler<GetNampPreDeploymentChecklistTemplatesQuery, ApplicationResult<List<NampPreDeploymentChecklistTemplateDto>>>
{
    private readonly INampPreDeploymentChecklistTemplateRepository _repo;

    public GetNampPreDeploymentChecklistTemplatesHandler(INampPreDeploymentChecklistTemplateRepository repo)
        => _repo = repo;

    public async Task<ApplicationResult<List<NampPreDeploymentChecklistTemplateDto>>> Handle(
        GetNampPreDeploymentChecklistTemplatesQuery request, CancellationToken ct = default)
    {
        var templates = await _repo.GetAllAsync(ct);
        var dtos = templates.Select(t => new NampPreDeploymentChecklistTemplateDto(
            t.Id, t.Title, t.Description, t.RequiresDocumentUpload,
            t.DocumentCategory?.ToString(), t.IsMandatory, t.SortOrder, t.IsActive, t.CreatedAt
        )).ToList();
        return ApplicationResult<List<NampPreDeploymentChecklistTemplateDto>>.Success(dtos);
    }
}
