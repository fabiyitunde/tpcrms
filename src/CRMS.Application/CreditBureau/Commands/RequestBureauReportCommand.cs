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
    CreditBureauProvider Provider = CreditBureauProvider.SmartComply,
    Guid? LoanApplicationId = null,
    bool IncludePdf = false
) : IRequest<ApplicationResult<BureauReportDto>>;

public class RequestBureauReportHandler : IRequestHandler<RequestBureauReportCommand, ApplicationResult<BureauReportDto>>
{
    private readonly IBureauReportRepository _repository;
    private readonly IConsentRecordRepository _consentRepository;
    private readonly ISmartComplyProvider _smartComplyProvider;
    private readonly IUnitOfWork _unitOfWork;

    public RequestBureauReportHandler(
        IBureauReportRepository repository,
        IConsentRecordRepository consentRepository,
        ISmartComplyProvider smartComplyProvider,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _consentRepository = consentRepository;
        _smartComplyProvider = smartComplyProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<BureauReportDto>> Handle(RequestBureauReportCommand request, CancellationToken ct = default)
    {
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

        var reportResult = BureauReport.Create(
            CreditBureauProvider.SmartComply,
            SubjectType.Individual,
            request.SubjectName,
            request.BVN,
            request.RequestedByUserId,
            request.ConsentRecordId,
            request.LoanApplicationId,
            taxId: null,
            partyId: null,
            partyType: null
        );

        if (reportResult.IsFailure)
            return ApplicationResult<BureauReportDto>.Failure(reportResult.Error);

        var report = reportResult.Value;
        report.MarkProcessing();

        await _repository.AddAsync(report, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var creditResult = await _smartComplyProvider.GetCRCFullAsync(request.BVN, ct);
        if (creditResult.IsFailure)
        {
            report.MarkFailed(creditResult.Error);
            _repository.Update(report);
            await _unitOfWork.SaveChangesAsync(ct);
            return ApplicationResult<BureauReportDto>.Failure(creditResult.Error);
        }

        var creditData = creditResult.Value;
        var summary = creditData.Summary;

        report.CompleteWithData(
            registryId: creditData.Id ?? request.BVN,
            creditScore: null,
            scoreGrade: null,
            reportDate: creditData.SearchedDate ?? DateTime.UtcNow,
            rawResponseJson: null,
            pdfReportBase64: null,
            totalAccounts: summary.TotalNoOfLoans,
            activeLoans: summary.TotalNoOfActiveLoans,
            performingAccounts: summary.TotalNoOfPerformingLoans,
            delinquentFacilities: summary.TotalNoOfDelinquentFacilities,
            closedAccounts: summary.TotalNoOfClosedLoans,
            totalOutstandingBalance: summary.TotalOutstanding,
            totalOverdue: summary.TotalOverdue,
            totalCreditLimit: summary.TotalBorrowed,
            maxDelinquencyDays: summary.MaxNoOfDays,
            hasLegalActions: false
        );

        foreach (var loan in creditData.LoanPerformance)
        {
            var status = loan.PerformanceStatus?.ToLowerInvariant() switch
            {
                "performing" => AccountStatus.Performing,
                "non-performing" or "nonperforming" or "delinquent" => AccountStatus.NonPerforming,
                "closed" => AccountStatus.Closed,
                "written off" or "writtenoff" => AccountStatus.WrittenOff,
                _ => AccountStatus.Unknown
            };

            var account = BureauAccount.Create(
                report.Id,
                loan.AccountNumber ?? "N/A",
                loan.LoanProvider,
                loan.Type,
                status,
                ParseDelinquencyLevel(0),
                loan.LoanAmount,
                loan.OutstandingBalance,
                null,
                loan.DateAccountOpened,
                null,
                loan.LastUpdatedAt,
                null,
                loan.PaymentProfile,
                LegalStatus.None,
                null,
                "NGN",
                loan.LastUpdatedAt ?? DateTime.UtcNow
            );
            report.AddAccount(account);
        }

        _repository.Update(report);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<BureauReportDto>.Success(MapToDto(report));
    }

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
        r.DelinquentFacilities,
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
