using CRMS.Application.Common;
using CRMS.Application.CreditBureau.DTOs;
using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CreditBureau.Commands;

public record RequestBureauReportCommand(
    string BVN,
    string SubjectName,
    Guid RequestedByUserId,
    Guid ConsentRecordId,
    CreditBureauProvider Provider = CreditBureauProvider.CreditRegistry,
    Guid? LoanApplicationId = null,
    bool IncludePdf = false
) : IRequest<ApplicationResult<BureauReportDto>>;

public class RequestBureauReportHandler : IRequestHandler<RequestBureauReportCommand, ApplicationResult<BureauReportDto>>
{
    private readonly IBureauReportRepository _repository;
    private readonly IConsentRecordRepository _consentRepository;
    private readonly ICreditBureauProvider _bureauProvider;
    private readonly IUnitOfWork _unitOfWork;

    public RequestBureauReportHandler(
        IBureauReportRepository repository,
        IConsentRecordRepository consentRepository,
        ICreditBureauProvider bureauProvider,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _consentRepository = consentRepository;
        _bureauProvider = bureauProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<BureauReportDto>> Handle(RequestBureauReportCommand request, CancellationToken ct = default)
    {
        // NDPA Compliance: Validate consent before any bureau access
        var consent = await _consentRepository.GetByIdAsync(request.ConsentRecordId, ct);
        if (consent == null)
            return ApplicationResult<BureauReportDto>.Failure(
                "Consent record not found. Bureau access requires valid borrower consent.");

        if (!consent.IsValid())
            return ApplicationResult<BureauReportDto>.Failure(
                $"Consent is {consent.Status}. Active consent is required for credit bureau checks.");

        if (consent.ConsentType != ConsentType.CreditBureauCheck)
            return ApplicationResult<BureauReportDto>.Failure(
                "Consent type does not authorize credit bureau access. Required: CreditBureauCheck.");

        // Create the report entity (consent ID is required for NDPA compliance)
        var reportResult = BureauReport.Create(
            request.Provider,
            SubjectType.Individual,
            request.SubjectName,
            request.BVN,
            request.RequestedByUserId,
            request.ConsentRecordId,
            request.LoanApplicationId,
            taxId: null,
            partyId: null, // Not available in this context
            partyType: null
        );

        if (reportResult.IsFailure)
            return ApplicationResult<BureauReportDto>.Failure(reportResult.Error);

        var report = reportResult.Value;
        report.MarkProcessing();

        await _repository.AddAsync(report, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // Search for subject by BVN
        var searchResult = await _bureauProvider.SearchByBVNAsync(request.BVN, ct);
        if (searchResult.IsFailure)
        {
            report.MarkFailed(searchResult.Error);
            _repository.Update(report);
            await _unitOfWork.SaveChangesAsync(ct);
            return ApplicationResult<BureauReportDto>.Failure(searchResult.Error);
        }

        if (!searchResult.Value.Found || string.IsNullOrEmpty(searchResult.Value.RegistryId))
        {
            report.MarkNotFound();
            _repository.Update(report);
            await _unitOfWork.SaveChangesAsync(ct);
            return ApplicationResult<BureauReportDto>.Failure("Subject not found in credit bureau");
        }

        // Get full credit report
        var reportDataResult = await _bureauProvider.GetCreditReportAsync(searchResult.Value.RegistryId, request.IncludePdf, ct);
        if (reportDataResult.IsFailure)
        {
            report.MarkFailed(reportDataResult.Error);
            _repository.Update(report);
            await _unitOfWork.SaveChangesAsync(ct);
            return ApplicationResult<BureauReportDto>.Failure(reportDataResult.Error);
        }

        var reportData = reportDataResult.Value;

        // Complete the report with data
        report.CompleteWithData(
            registryId: reportData.RegistryId,
            creditScore: reportData.CreditScore,
            scoreGrade: reportData.ScoreGrade,
            reportDate: reportData.ReportDate,
            rawResponseJson: reportData.RawJson,
            pdfReportBase64: reportData.PdfBase64,
            totalAccounts: reportData.Summary.TotalAccounts,
            activeLoans: reportData.Summary.ActiveLoans,
            performingAccounts: reportData.Summary.PerformingAccounts,
            nonPerformingAccounts: reportData.Summary.NonPerformingAccounts,
            closedAccounts: reportData.Summary.ClosedAccounts,
            totalOutstandingBalance: reportData.Summary.TotalOutstandingBalance,
            totalOverdue: reportData.Summary.TotalOverdue,
            totalCreditLimit: reportData.Summary.TotalCreditLimit,
            maxDelinquencyDays: reportData.Summary.MaxDelinquencyDays,
            hasLegalActions: reportData.Summary.HasLegalActions
        );

        // Add accounts
        foreach (var acc in reportData.Accounts)
        {
            var account = BureauAccount.Create(
                report.Id,
                acc.AccountNumber,
                acc.CreditorName,
                acc.AccountType,
                ParseAccountStatus(acc.Status),
                ParseDelinquencyLevel(acc.DelinquencyDays),
                acc.CreditLimit,
                acc.Balance,
                acc.MinimumPayment,
                acc.DateOpened,
                acc.DateClosed,
                acc.LastPaymentDate,
                acc.LastPaymentAmount,
                acc.PaymentProfile,
                ParseLegalStatus(acc.LegalStatus),
                acc.LegalStatusDate,
                acc.Currency,
                acc.LastUpdated
            );
            report.AddAccount(account);
        }

        // Add score factors
        foreach (var factor in reportData.ScoreFactors)
        {
            var scoreFactor = BureauScoreFactor.Create(
                report.Id,
                factor.FactorCode,
                factor.Description,
                factor.Impact,
                factor.Rank
            );
            report.AddScoreFactor(scoreFactor);
        }

        _repository.Update(report);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<BureauReportDto>.Success(MapToDto(report));
    }

    private static AccountStatus ParseAccountStatus(string status) => status switch
    {
        "Performing" => AccountStatus.Performing,
        "NonPerforming" => AccountStatus.NonPerforming,
        "WrittenOff" => AccountStatus.WrittenOff,
        "Closed" => AccountStatus.Closed,
        _ => AccountStatus.Unknown
    };

    private static DelinquencyLevel ParseDelinquencyLevel(int days) => days switch
    {
        0 => DelinquencyLevel.Current,
        <= 30 => DelinquencyLevel.Days1To30,
        <= 60 => DelinquencyLevel.Days31To60,
        <= 90 => DelinquencyLevel.Days61To90,
        <= 120 => DelinquencyLevel.Days91To120,
        <= 150 => DelinquencyLevel.Days121To150,
        <= 180 => DelinquencyLevel.Days151To180,
        <= 360 => DelinquencyLevel.Days181To360,
        _ => DelinquencyLevel.Over360Days
    };

    private static LegalStatus ParseLegalStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "litigation" => LegalStatus.Litigation,
        "foreclosure" => LegalStatus.Foreclosure,
        "bankruptcy" => LegalStatus.Bankruptcy,
        null or "" or "none" => LegalStatus.None,
        _ => LegalStatus.Other
    };

    private static BureauReportDto MapToDto(BureauReport r) => new(
        r.Id,
        r.LoanApplicationId,
        r.Provider.ToString(),
        r.SubjectType.ToString(),
        r.Status.ToString(),
        r.BVN,
        r.RegistryId,
        r.SubjectName,
        r.CreditScore,
        r.ScoreGrade,
        r.ReportDate,
        r.TotalAccounts,
        r.ActiveLoans,
        r.PerformingAccounts,
        r.NonPerformingAccounts,
        r.ClosedAccounts,
        r.TotalOutstandingBalance,
        r.TotalOverdue,
        r.TotalCreditLimit,
        r.MaxDelinquencyDays,
        r.HasLegalActions,
        r.RequestReference,
        r.RequestedAt,
        r.CompletedAt,
        r.ErrorMessage,
        r.FraudRiskScore,
        r.FraudRecommendation,
        r.PartyId,
        r.PartyType,
        r.Accounts.Select(a => new BureauAccountDto(
            a.Id, a.AccountNumber, a.CreditorName, a.AccountType, a.Status.ToString(),
            a.DelinquencyLevel.ToString(), a.CreditLimit, a.Balance, a.DateOpened,
            a.LastPaymentDate, a.PaymentProfile, a.LegalStatus.ToString()
        )).ToList(),
        r.ScoreFactors.Select(f => new BureauScoreFactorDto(
            f.Id, f.FactorCode, f.FactorDescription, f.Impact, f.Rank
        )).ToList()
    );
}
