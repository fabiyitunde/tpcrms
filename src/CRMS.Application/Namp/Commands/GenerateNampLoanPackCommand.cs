using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Application.Namp.Interfaces;
using CRMS.Application.Namp.Queries;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

public record GenerateNampLoanPackCommand(
    Guid NampApplicationId,
    Guid GeneratedByUserId,
    string GeneratedByUserName,
    string BankName,
    string BranchName
) : IRequest<ApplicationResult<byte[]>>;

public class GenerateNampLoanPackHandler
    : IRequestHandler<GenerateNampLoanPackCommand, ApplicationResult<byte[]>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IBureauReportRepository _bureauRepo;
    private readonly ICommitteeReviewRepository _committeeRepo;
    private readonly IUserRepository _userRepo;
    private readonly INampLoanPackGenerator _generator;

    public GenerateNampLoanPackHandler(
        INampApplicationRepository repo,
        IBureauReportRepository bureauRepo,
        ICommitteeReviewRepository committeeRepo,
        IUserRepository userRepo,
        INampLoanPackGenerator generator)
    {
        _repo = repo;
        _bureauRepo = bureauRepo;
        _committeeRepo = committeeRepo;
        _userRepo = userRepo;
        _generator = generator;
    }

    public async Task<ApplicationResult<byte[]>> Handle(
        GenerateNampLoanPackCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null)
            return ApplicationResult<byte[]>.Failure("NAMP application not found.");

        var techReport = await _repo.GetTechnicalAppraisalReportAsync(request.NampApplicationId, ct);
        var finReport  = await _repo.GetFinancialAppraisalReportAsync(request.NampApplicationId, ct);
        var bureauReports = await _bureauRepo.GetByNampApplicationIdAsync(request.NampApplicationId, ct);

        // Load committee review (most recent, if app has one)
        Domain.Aggregates.Committee.CommitteeReview? committeeReview = null;
        if (app.CurrentCommitteeReviewId.HasValue)
            committeeReview = await _committeeRepo.GetByIdAsync(app.CurrentCommitteeReviewId.Value, ct);

        // Resolve user names for status history
        var allUsers = await _userRepo.GetAllAsync(ct);
        var userNameLookup = allUsers.ToDictionary(u => u.Id, u => u.FullName);

        string ResolveName(Guid id) =>
            userNameLookup.TryGetValue(id, out var name) ? name : id.ToString()[..8] + "...";

        var statusHistory = app.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .Select(h => new NampLoanPackStatusEntry(
                h.Status.ToString(),
                h.ChangedAt,
                ResolveName(h.ChangedByUserId),
                h.Note))
            .ToList();

        var committeeMembers = committeeReview?.Members
            .OrderByDescending(m => m.IsChairperson)
            .ThenBy(m => m.UserName)
            .Select(m => new NampLoanPackMemberVote(
                m.UserName,
                m.Role,
                m.IsChairperson,
                m.Vote?.ToString(),
                m.VoteComment,
                m.VotedAt))
            .ToList() ?? new List<NampLoanPackMemberVote>();

        string? ratifiedByName = app.RatifiedByUserId.HasValue
            ? ResolveName(app.RatifiedByUserId.Value) : null;

        var dto = GetNampApplicationByIdHandler.MapToDto(app, techReport, finReport, bureauReports);

        var data = new NampLoanPackData(
            ApplicationNumber:    app.ApplicationNumber,
            ApplicationReference: app.ApplicationReference,
            Status:               app.Status.ToString(),
            CreatedAt:            app.CreatedAt,
            SubmittedAt:          app.SubmittedAt,
            RatifiedAt:           app.RatifiedAt,
            RatifiedByUserName:   ratifiedByName,
            GeneratedAt:          DateTime.UtcNow,
            GeneratedBy:          request.GeneratedByUserName,
            BankName:             request.BankName,
            BranchName:           request.BranchName,
            ApplicantName:        app.ApplicantName,
            BoaAccountNumber:     app.BoaAccountNumber,
            BoaAccountName:       app.BoaAccountName,
            ApplicantCategory:    app.ApplicantCategory.ToString(),
            ApplicantPhone:       app.ApplicantPhone,
            ApplicantEmail:       app.ApplicantEmail,
            Nin:                  app.Nin,
            Bvn:                  app.Bvn,
            DateOfBirth:          app.DateOfBirth,
            StateOfResidence:     app.StateOfResidence,
            LocalGovernmentArea:  app.LocalGovernmentArea,
            Occupation:           app.Occupation,
            EmployerName:         app.EmployerName,
            EmploymentStatus:     app.EmploymentStatus,
            YearsOfExperience:    app.YearsOfExperience,
            NumberOfDependants:   app.NumberOfDependants,
            EstimatedMonthlyIncome:  app.EstimatedMonthlyIncome,
            MonthlyLivingExpenses:   app.MonthlyLivingExpenses,
            EstimatedNetWorth:       app.EstimatedNetWorth,
            ExistingLoanObligations: app.ExistingLoanObligations,
            CompanyName:          app.CompanyName,
            RcNumber:             app.RcNumber,
            IndustrySector:       app.IndustrySector,
            EquipmentDescription: app.EquipmentDescription,
            EquipmentValue:       app.EquipmentValue,
            LoanPurpose:          app.LoanPurpose,
            EquityPercent:        app.EquityPercent,
            EquityAmount:         app.EquityAmount,
            LoanAmount:           app.LoanAmount,
            RequestedTenorMonths: app.RequestedTenorMonths,
            CommitteeTier:        app.CommitteeTier.ToString(),
            CommitteeDecision:    committeeReview?.FinalDecision?.ToString(),
            CommitteeApprovalVotes:   committeeReview?.ApprovalVotes ?? 0,
            CommitteeRejectionVotes:  committeeReview?.RejectionVotes ?? 0,
            CommitteeAbstainVotes:    committeeReview?.AbstainVotes ?? 0,
            CommitteeConditions:  committeeReview?.ApprovalConditions,
            CommitteeMembers:     committeeMembers,
            TechnicalAppraisal:   dto.TechnicalAppraisalReport,
            FinancialAppraisal:   dto.FinancialAppraisalReport,
            Guarantors:           dto.Guarantors,
            Collaterals:          dto.Collaterals,
            BureauReports:        dto.BureauReports,
            FinancialStatements:  dto.FinancialStatements,
            StatusHistory:        statusHistory,
            Documents:            dto.Documents
        );

        var bytes = await _generator.GenerateAsync(data, ct);
        return ApplicationResult<byte[]>.Success(bytes);
    }
}
