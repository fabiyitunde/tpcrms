using CRMS.Application.Common;
using CRMS.Application.LoanPack.DTOs;
using CRMS.Application.LoanPack.Interfaces;
using CRMS.Domain.Interfaces;
using LP = CRMS.Domain.Aggregates.LoanPack;

namespace CRMS.Application.LoanPack.Commands;

public record GenerateLoanPackCommand(
    Guid LoanApplicationId,
    Guid GeneratedByUserId,
    string GeneratedByUserName
) : IRequest<ApplicationResult<LoanPackResultDto>>;

public record LoanPackResultDto(
    Guid LoanPackId,
    string ApplicationNumber,
    int Version,
    string FileName,
    long FileSizeBytes,
    string Status,
    string? StoragePath = null
);

public class GenerateLoanPackHandler : IRequestHandler<GenerateLoanPackCommand, ApplicationResult<LoanPackResultDto>>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly ILoanProductRepository _productRepository;
    private readonly IBureauReportRepository _bureauRepository;
    private readonly IFinancialStatementRepository _financialRepository;
    private readonly IBankStatementRepository _bankStatementRepository;
    private readonly ICollateralRepository _collateralRepository;
    private readonly IGuarantorRepository _guarantorRepository;
    private readonly ICreditAdvisoryRepository _advisoryRepository;
    private readonly IWorkflowInstanceRepository _workflowRepository;
    private readonly ICommitteeReviewRepository _committeeRepository;
    private readonly ILoanPackRepository _loanPackRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILoanPackGenerator _pdfGenerator;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _unitOfWork;

    public GenerateLoanPackHandler(
        ILoanApplicationRepository loanAppRepository,
        ILoanProductRepository productRepository,
        IBureauReportRepository bureauRepository,
        IFinancialStatementRepository financialRepository,
        IBankStatementRepository bankStatementRepository,
        ICollateralRepository collateralRepository,
        IGuarantorRepository guarantorRepository,
        ICreditAdvisoryRepository advisoryRepository,
        IWorkflowInstanceRepository workflowRepository,
        ICommitteeReviewRepository committeeRepository,
        ILoanPackRepository loanPackRepository,
        IUserRepository userRepository,
        ILoanPackGenerator pdfGenerator,
        IFileStorageService fileStorage,
        IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _productRepository = productRepository;
        _bureauRepository = bureauRepository;
        _financialRepository = financialRepository;
        _bankStatementRepository = bankStatementRepository;
        _collateralRepository = collateralRepository;
        _guarantorRepository = guarantorRepository;
        _advisoryRepository = advisoryRepository;
        _workflowRepository = workflowRepository;
        _committeeRepository = committeeRepository;
        _loanPackRepository = loanPackRepository;
        _userRepository = userRepository;
        _pdfGenerator = pdfGenerator;
        _fileStorage = fileStorage;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<LoanPackResultDto>> Handle(GenerateLoanPackCommand request, CancellationToken ct = default)
    {
        // Load loan application (includes Documents, Parties, Comments, StatusHistory)
        var loanApp = await _loanAppRepository.GetByIdAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult<LoanPackResultDto>.Failure("Loan application not found");

        // Use MAX version so Failed records don't cause duplicate version numbers
        var maxExistingVersion = await _loanPackRepository.GetMaxVersionAsync(request.LoanApplicationId, ct);
        var nextVersion = maxExistingVersion + 1;

        var loanPackResult = LP.LoanPack.Create(
            request.LoanApplicationId,
            loanApp.ApplicationNumber,
            request.GeneratedByUserId,
            request.GeneratedByUserName,
            loanApp.CustomerName,
            loanApp.ProductCode,
            loanApp.RequestedAmount.Amount,
            version: nextVersion);

        if (!loanPackResult.IsSuccess)
            return ApplicationResult<LoanPackResultDto>.Failure(loanPackResult.Error);

        var loanPack = loanPackResult.Value;

        try
        {
            var packData = await BuildLoanPackDataAsync(loanApp, nextVersion, request.GeneratedByUserName, ct);

            var pdfBytes = await _pdfGenerator.GenerateAsync(packData, ct);

            var fileName = $"LoanPack_{loanApp.ApplicationNumber}_v{nextVersion}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
            var storagePath = $"loanpacks/{loanApp.ApplicationNumber}/{fileName}";

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = sha256.ComputeHash(pdfBytes);
            var contentHash = Convert.ToBase64String(hashBytes);

            loanPack.SetDocument(fileName, storagePath, pdfBytes.Length, contentHash);

            loanPack.SetContentSummary(
                packData.AIAdvisory?.RecommendedAmount,
                packData.AIAdvisory?.OverallRiskScore,
                packData.AIAdvisory?.RiskRating,
                packData.Directors.Count,
                packData.BureauReports.Count,
                packData.Collaterals.Count,
                packData.Guarantors.Count);

            loanPack.SetIncludedSections(
                executiveSummary: true,
                bureauReports: packData.BureauReports.Any(),
                financialAnalysis: packData.FinancialStatements.Any(),
                cashflowAnalysis: packData.CashflowAnalysis != null,
                collateralDetails: packData.Collaterals.Any(),
                guarantorDetails: packData.Guarantors.Any(),
                aiAdvisory: packData.AIAdvisory != null,
                workflowHistory: packData.ApprovalAuditTrail.Any(),
                committeeComments: packData.CommitteeComments.Any());

            var actualStoragePath = await _fileStorage.UploadAsync(
                containerName: "loanpacks",
                fileName: $"{loanApp.ApplicationNumber}/{fileName}",
                content: pdfBytes,
                contentType: "application/pdf",
                ct: ct);

            loanPack.SetDocument(fileName, actualStoragePath, pdfBytes.Length, contentHash);

            await _loanPackRepository.AddAsync(loanPack, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return ApplicationResult<LoanPackResultDto>.Success(new LoanPackResultDto(
                loanPack.Id,
                loanPack.ApplicationNumber,
                nextVersion,
                fileName,
                pdfBytes.Length,
                loanPack.Status.ToString(),
                actualStoragePath));
        }
        catch (Exception ex)
        {
            loanPack.MarkAsFailed(ex.Message);
            try
            {
                await _loanPackRepository.AddAsync(loanPack, ct);
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch { /* Best-effort audit record */ }

            return ApplicationResult<LoanPackResultDto>.Failure($"Failed to generate loan pack: {ex.Message}");
        }
    }

    private async Task<LoanPackData> BuildLoanPackDataAsync(
        Domain.Aggregates.LoanApplication.LoanApplication loanApp,
        int version,
        string generatedBy,
        CancellationToken ct)
    {
        // Load all related data sequentially — EF Core DbContext is not thread-safe.
        var loanAppWithParties  = await _loanAppRepository.GetByIdWithPartiesAsync(loanApp.Id, ct) ?? loanApp;
        var appWithChecklist    = await _loanAppRepository.GetByIdWithChecklistAsync(loanApp.Id, ct);
        var product             = await _productRepository.GetByIdAsync(loanApp.LoanProductId, ct);
        var bureauReports       = await _bureauRepository.GetByLoanApplicationIdWithDetailsAsync(loanApp.Id, ct);
        var financialStatements = await _financialRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var bankStatements      = await _bankStatementRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var collaterals         = await _collateralRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var guarantors          = await _guarantorRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var advisory            = await _advisoryRepository.GetLatestByLoanApplicationIdAsync(loanApp.Id, ct);
        // Hydrate JSON-backed collections that EF ignores on load
        advisory?.SetPersistedData(advisory.RiskScoresJson, advisory.RedFlagsJson, advisory.ConditionsJson, advisory.CovenantsJson);
        var workflow            = await _workflowRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);
        var committeeReview     = await _committeeRepository.GetByLoanApplicationIdAsync(loanApp.Id, ct);

        var productName = product?.Name ?? loanApp.ProductCode;

        // ── Customer profile ────────────────────────────────────────────────────
        var customerProfile = new CustomerProfileData(
            loanAppWithParties.CustomerName,
            loanAppWithParties.RegistrationNumber ?? "",
            loanAppWithParties.IncorporationDate,
            loanAppWithParties.IndustrySector ?? "",
            "",   // Sector — not stored on LoanApplication; populated from external customer master if available
            "",   // Address — not stored on LoanApplication
            "",   // Phone  — not stored on LoanApplication
            "",   // Email  — not stored on LoanApplication
            loanAppWithParties.AccountNumber,
            "",   // AccountType — not stored on LoanApplication
            null,
            null);

        // ── Application timeline ─────────────────────────────────────────────
        var timeline = new ApplicationTimelineData(
            loanApp.Status.ToString(),
            loanApp.Type.ToString(),
            loanApp.SubmittedAt,
            loanApp.BranchApprovedAt,
            loanApp.CreditCheckStartedAt,
            loanApp.CreditCheckCompletedAt,
            loanApp.FinalApprovedAt,
            loanApp.OfferIssuedAt,
            loanApp.OfferAcceptedAt,
            loanApp.CustomerSignedAt,
            loanApp.AcceptanceMethod?.ToString(),
            loanApp.KfsAcknowledged,
            loanApp.DisbursedAt,
            loanApp.CoreBankingLoanId);

        // ── Supporting documents ─────────────────────────────────────────────
        var documents = loanApp.Documents
            .OrderBy(d => d.Category.ToString())
            .ThenByDescending(d => d.UploadedAt)
            .Select(d => new DocumentRecord(
                d.FileName,
                d.Category.ToString(),
                d.Status.ToString(),
                d.UploadedAt,
                d.Description))
            .ToList();

        // ── Bureau lookup for director/signatory cross-referencing ───────────
        var bureauByPartyId = bureauReports
            .Where(b => b.PartyId.HasValue && b.Status == Domain.Enums.BureauReportStatus.Completed)
            .GroupBy(b => b.PartyId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.CompletedAt).First());

        // ── Directors ────────────────────────────────────────────────────────
        var directors = loanAppWithParties.Parties
            .Where(p => p.PartyType == Domain.Enums.PartyType.Director)
            .Select(d =>
            {
                bureauByPartyId.TryGetValue(d.Id, out var bureau);
                return new DirectorData(
                    d.FullName,
                    d.Designation ?? "",
                    d.BVN ?? "",
                    d.PhoneNumber ?? "",
                    d.Email ?? "",
                    d.ShareholdingPercent,
                    bureau?.CreditScore,
                    bureau?.ScoreGrade,
                    bureau != null && bureau.ActiveLoans > 0,
                    bureau != null && bureau.DelinquentFacilities > 0,
                    bureau != null
                        ? $"Score: {bureau.CreditScore?.ToString() ?? "N/A"} | Outstanding: {loanApp.RequestedAmount.Currency} {bureau.TotalOutstandingBalance:N0} | Delinquent facilities: {bureau.DelinquentFacilities}"
                        : null);
            }).ToList();

        // ── Signatories ──────────────────────────────────────────────────────
        var signatories = loanAppWithParties.Parties
            .Where(p => p.PartyType == Domain.Enums.PartyType.Signatory)
            .Select(s =>
            {
                bureauByPartyId.TryGetValue(s.Id, out var bureau);
                return new SignatoryData(
                    s.FullName,
                    s.Designation ?? "",
                    s.BVN ?? "",
                    s.PhoneNumber ?? "",
                    "",
                    bureau?.CreditScore,
                    bureau?.ScoreGrade,
                    bureau != null && bureau.ActiveLoans > 0,
                    bureau != null && bureau.DelinquentFacilities > 0);
            }).ToList();

        // ── Bureau reports ───────────────────────────────────────────────────
        var bureauData = bureauReports.Select(b =>
        {
            var activeLoans = b.Accounts
                .Where(a => a.Status == Domain.Enums.AccountStatus.Performing)
                .Select(a => new ActiveLoanData(
                    a.CreditorName ?? "",
                    a.AccountType ?? "",
                    a.CreditLimit,
                    a.Balance,
                    a.DateClosed,
                    a.Status.ToString()))
                .ToList();

            var delinquencies = b.Accounts
                .Where(a => a.DelinquencyLevel != Domain.Enums.DelinquencyLevel.Current)
                .Select(a => new DelinquencyData(
                    a.CreditorName ?? "",
                    a.AccountType ?? "",
                    a.Balance,
                    a.GetDelinquencyDays(),
                    a.DelinquencyLevel.ToString()))
                .ToList();

            return new BureauReportData(
                b.SubjectName,
                b.SubjectType.ToString(),
                b.Provider.ToString(),
                b.CompletedAt ?? b.RequestedAt,
                b.CreditScore,
                b.ScoreGrade,
                b.ActiveLoans,
                b.TotalOutstandingBalance,
                b.DelinquentFacilities,
                b.HasLegalActions,
                b.HasLegalActions ? $"Max delinquency: {b.MaxDelinquencyDays} days" : null,
                activeLoans,
                delinquencies);
        }).ToList();

        // ── Financial statements ─────────────────────────────────────────────
        var financialData = financialStatements
            .OrderByDescending(f => f.FinancialYear)
            .Select(f => new FinancialStatementData(
                f.FinancialYear,
                f.YearType.ToString(),
                f.AuditorName ?? "",
                f.BalanceSheet?.TotalAssets,
                f.BalanceSheet?.TotalCurrentAssets,
                f.BalanceSheet?.TotalNonCurrentAssets,
                f.BalanceSheet?.TotalLiabilities,
                f.BalanceSheet?.TotalCurrentLiabilities,
                f.BalanceSheet?.LongTermDebt,
                f.BalanceSheet?.TotalEquity,
                f.IncomeStatement?.Revenue,
                f.IncomeStatement?.GrossProfit,
                f.IncomeStatement?.OperatingProfit,
                f.IncomeStatement?.NetProfit,
                f.IncomeStatement?.EBITDA))
            .ToList();

        // ── Financial ratios ─────────────────────────────────────────────────
        var latestFinancial = financialStatements.OrderByDescending(f => f.FinancialYear).FirstOrDefault();
        FinancialRatiosData? ratiosData = null;
        if (latestFinancial?.CalculatedRatios != null)
        {
            var r = latestFinancial.CalculatedRatios;
            var prevFinancial = financialStatements.OrderByDescending(f => f.FinancialYear).Skip(1).FirstOrDefault();
            decimal? revenueGrowth = null;
            decimal? profitGrowth = null;
            if (prevFinancial?.IncomeStatement != null && latestFinancial.IncomeStatement != null
                && prevFinancial.IncomeStatement.Revenue > 0)
            {
                revenueGrowth = (latestFinancial.IncomeStatement.Revenue - prevFinancial.IncomeStatement.Revenue)
                    / prevFinancial.IncomeStatement.Revenue * 100;
            }
            if (prevFinancial?.IncomeStatement != null && latestFinancial.IncomeStatement != null
                && prevFinancial.IncomeStatement.NetProfit != 0)
            {
                profitGrowth = (latestFinancial.IncomeStatement.NetProfit - prevFinancial.IncomeStatement.NetProfit)
                    / Math.Abs(prevFinancial.IncomeStatement.NetProfit) * 100;
            }

            ratiosData = new FinancialRatiosData(
                r.CurrentRatio, r.QuickRatio, r.CashRatio,
                r.DebtToEquityRatio, r.DebtToAssetsRatio, r.InterestCoverageRatio,
                r.GrossMarginPercent, r.OperatingMarginPercent, r.NetProfitMarginPercent,
                r.ReturnOnAssets, r.ReturnOnEquity,
                r.AssetTurnover, r.InventoryTurnover, r.ReceivablesDays, r.PayablesDays,
                r.DebtServiceCoverageRatio,
                revenueGrowth, profitGrowth);
        }

        // ── Cashflow analysis ────────────────────────────────────────────────
        CashflowAnalysisData? cashflowData = null;
        var analysedStatements = bankStatements.Where(b => b.CashflowSummary != null).ToList();
        if (analysedStatements.Any())
        {
            var totalMonths = analysedStatements.Sum(b => b.CashflowSummary!.PeriodMonths);
            var avgMonthlyInflow = totalMonths > 0
                ? analysedStatements.Sum(b => b.CashflowSummary!.TotalCredits) / totalMonths : 0;
            var avgMonthlyOutflow = totalMonths > 0
                ? analysedStatements.Sum(b => b.CashflowSummary!.TotalDebits) / totalMonths : 0;
            var netCashflow = avgMonthlyInflow - avgMonthlyOutflow;
            var lowestBalance = analysedStatements.Min(b => b.CashflowSummary!.LowestBalance);
            var highestBalance = analysedStatements.Max(b => b.CashflowSummary!.HighestBalance);
            var avgBalance = analysedStatements.Average(b => b.CashflowSummary!.AverageMonthlyBalance);
            var totalBounced = analysedStatements.Sum(b => b.CashflowSummary!.BouncedTransactionCount);
            var balanceVol = analysedStatements.Average(b => b.CashflowSummary!.BalanceVolatility);
            var incomeVol = analysedStatements.Average(b => b.CashflowSummary!.IncomeVolatility);
            var loanRepayments = analysedStatements.Sum(b => b.CashflowSummary!.DetectedLoanRepayments);
            var rentUtils = analysedStatements.Sum(b => b.CashflowSummary!.DetectedRentPayments + b.CashflowSummary!.DetectedUtilityPayments);
            var salaryIn = analysedStatements.Sum(b => b.CashflowSummary!.DetectedMonthlySalary ?? 0);
            var businessIn = avgMonthlyInflow - (salaryIn / Math.Max(totalMonths, 1));
            var trustLevel = balanceVol < 0.2m ? "High" : balanceVol < 0.5m ? "Medium" : "Low";

            cashflowData = new CashflowAnalysisData(
                totalMonths,
                Math.Round(avgMonthlyInflow, 2),
                Math.Round(avgMonthlyOutflow, 2),
                Math.Round(netCashflow, 2),
                Math.Round(lowestBalance, 2),
                Math.Round(highestBalance, 2),
                Math.Round(avgBalance, 2),
                Math.Round(salaryIn, 2),
                Math.Round(businessIn, 2),
                0,
                Math.Round(loanRepayments, 2),
                Math.Round(rentUtils, 2),
                0,
                avgMonthlyOutflow - loanRepayments - rentUtils > 0
                    ? Math.Round(avgMonthlyOutflow - loanRepayments - rentUtils, 2) : 0,
                Math.Round(incomeVol, 4),
                Math.Round(balanceVol, 4),
                totalBounced,
                0,
                0,
                0,
                trustLevel);
        }

        // ── Collaterals ──────────────────────────────────────────────────────
        var collateralData = collaterals.Select(c =>
        {
            var latestValuation = c.Valuations.OrderByDescending(v => v.ValuationDate).FirstOrDefault();
            return new CollateralData(
                c.Type.ToString(),
                c.Description,
                c.Location ?? "",
                c.MarketValue?.Amount ?? 0,
                c.ForcedSaleValue?.Amount ?? 0,
                c.AcceptableValue?.Amount ?? 0,
                latestValuation?.ValuationDate.ToString("dd-MMM-yyyy") ?? "",
                latestValuation != null
                    ? $"{latestValuation.ValuerName ?? ""}{(string.IsNullOrWhiteSpace(latestValuation.ValuerCompany) ? "" : $" ({latestValuation.ValuerCompany})")}"
                    : "",
                c.Status.ToString(),
                c.LienType?.ToString() ?? "",
                c.LienReference,
                c.InsurancePolicyNumber,
                c.InsuranceExpiryDate,
                c.IsLegalCleared,
                c.LegalClearedAt);
        }).ToList();

        var totalCollateralValue = collaterals.Sum(c => c.AcceptableValue?.Amount ?? 0);
        var approvedOrRequested = loanApp.ApprovedAmount?.Amount ?? loanApp.RequestedAmount.Amount;
        var collateralCoverage = approvedOrRequested > 0 ? totalCollateralValue / approvedOrRequested : 0;

        // ── Guarantors ───────────────────────────────────────────────────────
        var guarantorData = guarantors.Select(g => new GuarantorData(
            g.FullName,
            g.Type.ToString(),
            g.RelationshipToApplicant ?? "",
            g.Address ?? "",
            g.Phone ?? "",
            g.DeclaredNetWorth?.Amount ?? 0,
            g.GuaranteeLimit?.Amount ?? 0,
            g.CreditScore,
            g.CreditScoreGrade,
            g.Status.ToString(),
            false, false))   // Guarantor bureau lookup not yet implemented
            .ToList();

        var totalGuaranteeAmount = guarantors.Sum(g => g.GuaranteeLimit?.Amount ?? 0);

        // ── AI Advisory ──────────────────────────────────────────────────────
        AIAdvisoryData? aiData = null;
        if (advisory != null)
        {
            var mitigatingFactorsList = string.IsNullOrWhiteSpace(advisory.MitigatingFactors)
                ? new List<string>()
                : advisory.MitigatingFactors
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

            var scoreBreakdown = advisory.RiskScores
                .OrderBy(s => s.Category)
                .Select(s => new AdvisoryScoreItem(
                    s.Category.ToString(),
                    (int)s.Score,
                    s.Rating.ToString(),
                    s.Rationale))
                .ToList();

            aiData = new AIAdvisoryData(
                (int)advisory.OverallScore,
                advisory.OverallRating.ToString(),
                advisory.ExecutiveSummary ?? "",
                advisory.Recommendation.ToString(),
                advisory.HasCriticalRedFlags,
                scoreBreakdown,
                advisory.StrengthsAnalysis,
                advisory.WeaknessesAnalysis,
                advisory.KeyRisks,
                advisory.MitigatingFactors,
                advisory.RecommendedAmount.HasValue
                    ? $"Recommend {loanApp.RequestedAmount.Currency} {advisory.RecommendedAmount:N0}" : "",
                advisory.RecommendedAmount,
                advisory.RecommendedTenorMonths.HasValue
                    ? $"{advisory.RecommendedTenorMonths} months" : "",
                advisory.RecommendedTenorMonths,
                advisory.RecommendedInterestRate.HasValue
                    ? $"{advisory.RecommendedInterestRate:N2}% per annum" : "",
                advisory.RecommendedInterestRate,
                "",
                advisory.RedFlags.ToList(),
                mitigatingFactorsList,
                advisory.Conditions.ToList(),
                advisory.Covenants.ToList(),
                advisory.GeneratedAt,
                advisory.ModelVersion);
        }

        // ── Resolve all actor/author names in one lookup ────────────────────
        var allActorIds = loanApp.StatusHistory.Select(h => h.ChangedByUserId)
            .Concat(loanApp.Comments.Select(c => c.UserId))
            .Concat(workflow?.TransitionHistory.Select(t => t.PerformedByUserId) ?? Enumerable.Empty<Guid>())
            .Distinct().ToList();
        var allUsers = await _userRepository.GetAllAsync(ct);
        var userNameLookup = allUsers
            .Where(u => allActorIds.Contains(u.Id))
            .ToDictionary(u => u.Id, u => u.FullName);

        // ── Approval audit trail (from LoanApplicationStatusHistory) ────────
        var approvalAuditTrail = loanApp.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .Select(h => new ApprovalAuditEntry(
                h.ChangedAt,
                h.Status.ToString(),
                h.Comment,
                userNameLookup.TryGetValue(h.ChangedByUserId, out var name) ? name : h.ChangedByUserId.ToString()))
            .ToList();

        // ── Credit officer notes ─────────────────────────────────────────────
        var creditOfficerNotes = loanApp.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c => new ApplicationCommentData(
                c.CreatedAt,
                c.Content,
                c.Category,
                userNameLookup.TryGetValue(c.UserId, out var authorName) ? authorName : c.UserId.ToString()))
            .ToList();

        // ── Workflow history (raw transition log) ────────────────────────────
        var workflowHistory = workflow?.TransitionHistory
            .OrderBy(t => t.PerformedAt)
            .Select(t => new WorkflowHistoryData(
                t.PerformedAt,
                t.FromStatus?.ToString() ?? "",
                t.ToStatus.ToString(),
                t.Action.ToString(),
                userNameLookup.TryGetValue(t.PerformedByUserId, out var wfName) ? wfName : t.PerformedByUserId.ToString(),
                t.Comment))
            .ToList() ?? new List<WorkflowHistoryData>();

        // ── Committee comments ───────────────────────────────────────────────
        var memberLookup = committeeReview?.Members
            .ToDictionary(m => m.UserId, m => (m.UserName, m.Role))
            ?? new Dictionary<Guid, (string, string)>();

        var committeeComments = committeeReview?.Comments
            .OrderBy(c => c.CreatedAt)
            .Select(c =>
            {
                memberLookup.TryGetValue(c.UserId, out var member);
                return new CommitteeCommentData(
                    c.CreatedAt,
                    string.IsNullOrWhiteSpace(member.UserName) ? c.UserId.ToString() : member.UserName,
                    member.Role ?? "",
                    c.Content,
                    null,
                    c.Visibility.ToString());
            })
            .ToList() ?? new List<CommitteeCommentData>();

        // ── Committee decision ───────────────────────────────────────────────
        CommitteeDecisionData? committeeDecision = null;
        if (committeeReview != null)
        {
            var memberVotes = committeeReview.Members
                .OrderBy(m => m.UserName)
                .Select(m => new CommitteeMemberVoteData(
                    m.UserName,
                    m.Role,
                    m.Vote?.ToString(),
                    m.VoteComment,
                    m.VotedAt))
                .ToList();

            committeeDecision = new CommitteeDecisionData(
                committeeReview.FinalDecision?.ToString() ?? "Pending",
                committeeReview.ApprovalVotes,
                committeeReview.RejectionVotes,
                committeeReview.AbstainVotes,
                committeeReview.PendingVotes,
                committeeReview.DecisionRationale,
                committeeReview.RecommendedAmount,
                committeeReview.RecommendedTenorMonths,
                committeeReview.RecommendedInterestRate,
                memberVotes);
        }

        // ── Conditions of approval ───────────────────────────────────────────
        var approvalConditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(committeeReview?.ApprovalConditions))
        {
            approvalConditions.AddRange(committeeReview.ApprovalConditions
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        // ── Disbursement checklist ───────────────────────────────────────────
        var disbursementChecklist = appWithChecklist?.ChecklistItems
            .OrderBy(c => c.ConditionType)
            .ThenBy(c => c.SortOrder)
            .Select(c => new ChecklistItemData(
                c.ItemName,
                c.Description,
                c.ConditionType.ToString(),
                c.IsMandatory,
                c.CanBeWaived,
                c.Status.ToString(),
                c.SatisfiedAt,
                c.WaiverReason,
                c.WaiverProposedAt,
                c.DueDate))
            .ToList() ?? new List<ChecklistItemData>();

        return new LoanPackData(
            loanApp.ApplicationNumber,
            loanApp.CreatedAt,
            productName,
            loanApp.ProductCode,
            loanApp.RequestedAmount.Amount,
            loanApp.RequestedAmount.Currency,
            loanApp.RequestedTenorMonths,
            loanApp.InterestRatePerAnnum,
            loanApp.Purpose ?? "",
            customerProfile,
            timeline,
            directors,
            signatories,
            documents,
            bureauData,
            financialData,
            ratiosData,
            cashflowData,
            collateralData,
            totalCollateralValue,
            collateralCoverage,
            guarantorData,
            totalGuaranteeAmount,
            aiData,
            approvalAuditTrail,
            committeeComments,
            approvalConditions,
            loanApp.ApprovedAmount?.Amount,
            loanApp.ApprovedTenorMonths,
            loanApp.ApprovedInterestRate,
            committeeDecision,
            disbursementChecklist,
            creditOfficerNotes,
            workflowHistory,
            DateTime.UtcNow,
            generatedBy,
            version);
    }
}
