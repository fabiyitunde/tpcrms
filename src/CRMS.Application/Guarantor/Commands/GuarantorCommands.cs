using CRMS.Application.Common;
using CRMS.Application.Guarantor.DTOs;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.ValueObjects;
using G = CRMS.Domain.Aggregates.Guarantor;

namespace CRMS.Application.Guarantor.Commands;

public record AddIndividualGuarantorCommand(
    Guid LoanApplicationId,
    string FullName,
    string? BVN,
    GuaranteeType GuaranteeType,
    Guid CreatedByUserId,
    string? RelationshipToApplicant = null,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    decimal? GuaranteeLimit = null,
    string Currency = "NGN",
    bool IsDirector = false,
    bool IsShareholder = false,
    decimal? ShareholdingPercentage = null,
    decimal? DeclaredNetWorth = null,
    string? Occupation = null,
    string? EmployerName = null,
    decimal? MonthlyIncome = null
) : IRequest<ApplicationResult<GuarantorDto>>;

public class AddIndividualGuarantorHandler : IRequestHandler<AddIndividualGuarantorCommand, ApplicationResult<GuarantorDto>>
{
    private readonly IGuarantorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddIndividualGuarantorHandler(IGuarantorRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<GuarantorDto>> Handle(AddIndividualGuarantorCommand request, CancellationToken ct = default)
    {
        var guaranteeLimit = request.GuaranteeLimit.HasValue 
            ? Money.Create(request.GuaranteeLimit.Value, request.Currency) 
            : null;

        var result = G.Guarantor.CreateIndividual(
            request.LoanApplicationId,
            request.FullName,
            request.BVN,
            request.GuaranteeType,
            request.CreatedByUserId,
            request.RelationshipToApplicant,
            request.Email,
            request.Phone,
            request.Address,
            guaranteeLimit
        );

        if (result.IsFailure)
            return ApplicationResult<GuarantorDto>.Failure(result.Error);

        var guarantor = result.Value;

        // Set additional details
        if (request.IsDirector || request.IsShareholder)
        {
            guarantor.SetDirectorDetails(request.IsDirector, request.IsShareholder, request.ShareholdingPercentage);
        }

        if (request.DeclaredNetWorth.HasValue || request.Occupation != null)
        {
            var netWorth = request.DeclaredNetWorth.HasValue ? Money.Create(request.DeclaredNetWorth.Value, request.Currency) : null;
            var income = request.MonthlyIncome.HasValue ? Money.Create(request.MonthlyIncome.Value, request.Currency) : null;
            guarantor.SetFinancialDetails(netWorth, request.Occupation, request.EmployerName, income);
        }

        await _repository.AddAsync(guarantor, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<GuarantorDto>.Success(MapToDto(guarantor));
    }

    private static GuarantorDto MapToDto(G.Guarantor g) => new(
        g.Id, g.LoanApplicationId, g.GuarantorReference, g.Type.ToString(), g.Status.ToString(),
        g.GuaranteeType.ToString(), g.FullName, g.BVN, g.CompanyName, g.RegistrationNumber,
        g.Email, g.Phone, g.Address, g.RelationshipToApplicant, g.IsDirector, g.IsShareHolder,
        g.ShareholdingPercentage, g.DeclaredNetWorth?.Amount, g.VerifiedNetWorth?.Amount,
        g.Occupation, g.EmployerName, g.MonthlyIncome?.Amount, g.DeclaredNetWorth?.Currency,
        g.GuaranteeLimit?.Amount, g.IsUnlimited, g.GuaranteeStartDate, g.GuaranteeEndDate,
        g.BureauReportId, g.CreditScore, g.CreditScoreGrade, g.CreditCheckDate, g.HasCreditIssues,
        g.CreditIssuesSummary, g.ExistingGuaranteeCount, g.TotalExistingGuarantees?.Amount,
        g.HasSignedGuaranteeAgreement, g.AgreementSignedDate, g.CreatedAt, g.ApprovedAt, g.RejectionReason,
        g.Documents.Select(d => new GuarantorDocumentDto(d.Id, d.DocumentType, d.FileName,
            d.FileSizeBytes, d.IsVerified, d.UploadedAt)).ToList()
    );
}

public record RunGuarantorCreditCheckCommand(
    Guid GuarantorId,
    Guid RequestedByUserId,
    Guid ConsentRecordId
) : IRequest<ApplicationResult<GuarantorCreditCheckResultDto>>;

public class RunGuarantorCreditCheckHandler : IRequestHandler<RunGuarantorCreditCheckCommand, ApplicationResult<GuarantorCreditCheckResultDto>>
{
    private readonly IGuarantorRepository _guarantorRepository;
    private readonly ICreditBureauProvider _bureauProvider;
    private readonly IBureauReportRepository _bureauReportRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RunGuarantorCreditCheckHandler(
        IGuarantorRepository guarantorRepository,
        ICreditBureauProvider bureauProvider,
        IBureauReportRepository bureauReportRepository,
        IUnitOfWork unitOfWork)
    {
        _guarantorRepository = guarantorRepository;
        _bureauProvider = bureauProvider;
        _bureauReportRepository = bureauReportRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<GuarantorCreditCheckResultDto>> Handle(RunGuarantorCreditCheckCommand request, CancellationToken ct = default)
    {
        var guarantor = await _guarantorRepository.GetByIdAsync(request.GuarantorId, ct);
        if (guarantor == null)
            return ApplicationResult<GuarantorCreditCheckResultDto>.Failure("Guarantor not found");

        if (string.IsNullOrEmpty(guarantor.BVN))
            return ApplicationResult<GuarantorCreditCheckResultDto>.Failure("BVN is required for credit check");

        // Search bureau
        var searchResult = await _bureauProvider.SearchByBVNAsync(guarantor.BVN, ct);
        if (searchResult.IsFailure)
            return ApplicationResult<GuarantorCreditCheckResultDto>.Failure(searchResult.Error);

        if (!searchResult.Value.Found || string.IsNullOrEmpty(searchResult.Value.RegistryId))
        {
            guarantor.RecordCreditCheck(Guid.Empty, null, null, false, "No credit history found", 0, null);
            _guarantorRepository.Update(guarantor);
            await _unitOfWork.SaveChangesAsync(ct);
            
            return ApplicationResult<GuarantorCreditCheckResultDto>.Success(new GuarantorCreditCheckResultDto(
                guarantor.Id, Guid.Empty, null, null, false, "No credit history found", 0, null
            ));
        }

        // Get credit report
        var reportResult = await _bureauProvider.GetCreditReportAsync(searchResult.Value.RegistryId, false, ct);
        if (reportResult.IsFailure)
            return ApplicationResult<GuarantorCreditCheckResultDto>.Failure(reportResult.Error);

        var report = reportResult.Value;

        // Analyze credit issues
        var hasCreditIssues = report.Summary.NonPerformingAccounts > 0 || 
                             report.Summary.MaxDelinquencyDays > 60 ||
                             report.Summary.HasLegalActions;

        var issuesSummary = hasCreditIssues
            ? $"NPL: {report.Summary.NonPerformingAccounts}, Max Delinquency: {report.Summary.MaxDelinquencyDays} days, Legal: {report.Summary.HasLegalActions}"
            : "No significant issues";

        // Get existing guarantee count for this BVN
        var existingGuaranteeCount = await _guarantorRepository.GetActiveGuaranteeCountByBVNAsync(guarantor.BVN, ct);

        // Create bureau report entity (consent required for NDPA compliance)
        var bureauReportResult = Domain.Aggregates.CreditBureau.BureauReport.Create(
            CreditBureauProvider.CreditRegistry,
            SubjectType.Individual,
            guarantor.FullName,
            guarantor.BVN,
            request.RequestedByUserId,
            request.ConsentRecordId,
            guarantor.LoanApplicationId
        );

        if (bureauReportResult.IsSuccess)
        {
            var bureauReport = bureauReportResult.Value;
            bureauReport.CompleteWithData(
                report.RegistryId, report.CreditScore, report.ScoreGrade, report.ReportDate,
                report.RawJson, null, report.Summary.TotalAccounts, report.Summary.PerformingAccounts,
                report.Summary.NonPerformingAccounts, report.Summary.ClosedAccounts,
                report.Summary.TotalOutstandingBalance, report.Summary.TotalCreditLimit,
                report.Summary.MaxDelinquencyDays, report.Summary.HasLegalActions
            );
            await _bureauReportRepository.AddAsync(bureauReport, ct);

            var totalExisting = Money.Create(report.Summary.TotalOutstandingBalance, "NGN");
            guarantor.RecordCreditCheck(
                bureauReport.Id, report.CreditScore, report.ScoreGrade,
                hasCreditIssues, issuesSummary, existingGuaranteeCount, totalExisting
            );

            _guarantorRepository.Update(guarantor);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApplicationResult<GuarantorCreditCheckResultDto>.Success(new GuarantorCreditCheckResultDto(
                guarantor.Id, bureauReport.Id, report.CreditScore, report.ScoreGrade,
                hasCreditIssues, issuesSummary, existingGuaranteeCount, report.Summary.TotalOutstandingBalance
            ));
        }

        return ApplicationResult<GuarantorCreditCheckResultDto>.Failure("Failed to create bureau report");
    }
}

public record ApproveGuarantorCommand(Guid GuarantorId, Guid ApprovedByUserId, decimal? VerifiedNetWorth = null, string Currency = "NGN") 
    : IRequest<ApplicationResult<GuarantorDto>>;

public class ApproveGuarantorHandler : IRequestHandler<ApproveGuarantorCommand, ApplicationResult<GuarantorDto>>
{
    private readonly IGuarantorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveGuarantorHandler(IGuarantorRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<GuarantorDto>> Handle(ApproveGuarantorCommand request, CancellationToken ct = default)
    {
        var guarantor = await _repository.GetByIdWithDetailsAsync(request.GuarantorId, ct);
        if (guarantor == null)
            return ApplicationResult<GuarantorDto>.Failure("Guarantor not found");

        var verifiedNetWorth = request.VerifiedNetWorth.HasValue 
            ? Money.Create(request.VerifiedNetWorth.Value, request.Currency) 
            : null;

        var result = guarantor.Approve(request.ApprovedByUserId, verifiedNetWorth);
        if (result.IsFailure)
            return ApplicationResult<GuarantorDto>.Failure(result.Error);

        _repository.Update(guarantor);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<GuarantorDto>.Success(MapToDto(guarantor));
    }

    private static GuarantorDto MapToDto(G.Guarantor g) => new(
        g.Id, g.LoanApplicationId, g.GuarantorReference, g.Type.ToString(), g.Status.ToString(),
        g.GuaranteeType.ToString(), g.FullName, g.BVN, g.CompanyName, g.RegistrationNumber,
        g.Email, g.Phone, g.Address, g.RelationshipToApplicant, g.IsDirector, g.IsShareHolder,
        g.ShareholdingPercentage, g.DeclaredNetWorth?.Amount, g.VerifiedNetWorth?.Amount,
        g.Occupation, g.EmployerName, g.MonthlyIncome?.Amount, g.DeclaredNetWorth?.Currency,
        g.GuaranteeLimit?.Amount, g.IsUnlimited, g.GuaranteeStartDate, g.GuaranteeEndDate,
        g.BureauReportId, g.CreditScore, g.CreditScoreGrade, g.CreditCheckDate, g.HasCreditIssues,
        g.CreditIssuesSummary, g.ExistingGuaranteeCount, g.TotalExistingGuarantees?.Amount,
        g.HasSignedGuaranteeAgreement, g.AgreementSignedDate, g.CreatedAt, g.ApprovedAt, g.RejectionReason,
        g.Documents.Select(d => new GuarantorDocumentDto(d.Id, d.DocumentType, d.FileName,
            d.FileSizeBytes, d.IsVerified, d.UploadedAt)).ToList()
    );
}

public record RejectGuarantorCommand(Guid GuarantorId, Guid RejectedByUserId, string Reason) : IRequest<ApplicationResult<GuarantorDto>>;

public class RejectGuarantorHandler : IRequestHandler<RejectGuarantorCommand, ApplicationResult<GuarantorDto>>
{
    private readonly IGuarantorRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectGuarantorHandler(IGuarantorRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<GuarantorDto>> Handle(RejectGuarantorCommand request, CancellationToken ct = default)
    {
        var guarantor = await _repository.GetByIdWithDetailsAsync(request.GuarantorId, ct);
        if (guarantor == null)
            return ApplicationResult<GuarantorDto>.Failure("Guarantor not found");

        var result = guarantor.Reject(request.RejectedByUserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult<GuarantorDto>.Failure(result.Error);

        _repository.Update(guarantor);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<GuarantorDto>.Success(MapToDto(guarantor));
    }

    private static GuarantorDto MapToDto(G.Guarantor g) => new(
        g.Id, g.LoanApplicationId, g.GuarantorReference, g.Type.ToString(), g.Status.ToString(),
        g.GuaranteeType.ToString(), g.FullName, g.BVN, g.CompanyName, g.RegistrationNumber,
        g.Email, g.Phone, g.Address, g.RelationshipToApplicant, g.IsDirector, g.IsShareHolder,
        g.ShareholdingPercentage, g.DeclaredNetWorth?.Amount, g.VerifiedNetWorth?.Amount,
        g.Occupation, g.EmployerName, g.MonthlyIncome?.Amount, g.DeclaredNetWorth?.Currency,
        g.GuaranteeLimit?.Amount, g.IsUnlimited, g.GuaranteeStartDate, g.GuaranteeEndDate,
        g.BureauReportId, g.CreditScore, g.CreditScoreGrade, g.CreditCheckDate, g.HasCreditIssues,
        g.CreditIssuesSummary, g.ExistingGuaranteeCount, g.TotalExistingGuarantees?.Amount,
        g.HasSignedGuaranteeAgreement, g.AgreementSignedDate, g.CreatedAt, g.ApprovedAt, g.RejectionReason,
        g.Documents.Select(d => new GuarantorDocumentDto(d.Id, d.DocumentType, d.FileName,
            d.FileSizeBytes, d.IsVerified, d.UploadedAt)).ToList()
    );
}
