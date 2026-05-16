using CRMS.Application.Common;
using CRMS.Application.Workflow.Interfaces;
using CRMS.Domain.Aggregates.FinancialStatement;
using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Aggregates.StatementAnalysis;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Workflow.Queries;

public record GateItem(
    Guid ItemId,
    string ItemType,
    string ItemLabel,
    string State,           // "Rejected" | "Pending"
    string? RejectionReason
);

public record ApprovalGateResult(
    bool IsStrict,
    List<GateItem> RejectedItems,
    List<GateItem> PendingItems
)
{
    public bool HasIssues => RejectedItems.Count > 0 || PendingItems.Count > 0;
    public bool IsHardBlock => IsStrict && HasIssues;
    public bool RequiresOverrideNote => !IsStrict && RejectedItems.Count > 0;
}

/// <summary>
/// Stage values: "BranchReview", "HOReview", "CreditAnalysis", "FinalApproval"
/// </summary>
public record CheckApprovalGateQuery(Guid LoanApplicationId, string Stage)
    : IRequest<ApplicationResult<ApprovalGateResult>>;

public class CheckApprovalGateHandler : IRequestHandler<CheckApprovalGateQuery, ApplicationResult<ApprovalGateResult>>
{
    private readonly ILoanApplicationRepository _loanAppRepo;
    private readonly IBankStatementRepository _bankStatementRepo;
    private readonly IFinancialStatementRepository _financialStatementRepo;
    private readonly ICollateralRepository _collateralRepo;
    private readonly IGuarantorRepository _guarantorRepo;
    private readonly IApprovalGateConfig _gateConfig;

    public CheckApprovalGateHandler(
        ILoanApplicationRepository loanAppRepo,
        IBankStatementRepository bankStatementRepo,
        IFinancialStatementRepository financialStatementRepo,
        ICollateralRepository collateralRepo,
        IGuarantorRepository guarantorRepo,
        IApprovalGateConfig gateConfig)
    {
        _loanAppRepo = loanAppRepo;
        _bankStatementRepo = bankStatementRepo;
        _financialStatementRepo = financialStatementRepo;
        _collateralRepo = collateralRepo;
        _guarantorRepo = guarantorRepo;
        _gateConfig = gateConfig;
    }

    public async Task<ApplicationResult<ApprovalGateResult>> Handle(CheckApprovalGateQuery request, CancellationToken ct = default)
    {
        bool isStrict = _gateConfig.IsStrict(request.Stage);
        var rejected = new List<GateItem>();
        var pending = new List<GateItem>();

        bool checkDocuments = request.Stage is "BranchReview" or "HOReview" or "FinalApproval";
        bool checkBankStatements = request.Stage is "BranchReview" or "HOReview" or "CreditAnalysis" or "FinalApproval";
        bool checkFinancials = request.Stage is "BranchReview" or "HOReview" or "FinalApproval";
        bool checkCollateral = request.Stage is "CreditAnalysis" or "FinalApproval";
        bool checkGuarantors = request.Stage is "CreditAnalysis" or "FinalApproval";
        bool checkLegalClearance = request.Stage is "LegalReview";

        if (checkDocuments)
        {
            var app = await _loanAppRepo.GetByIdAsync(request.LoanApplicationId, ct);
            if (app == null)
                return ApplicationResult<ApprovalGateResult>.Failure("Application not found");

            foreach (var doc in app.Documents)
            {
                if (doc.Status == DocumentStatus.Rejected)
                    rejected.Add(new GateItem(doc.Id, "Document", doc.FileName, "Rejected", doc.RejectionReason));
                else if (doc.Status == DocumentStatus.Uploaded)
                    pending.Add(new GateItem(doc.Id, "Document", doc.FileName, "Pending", null));
            }
        }

        if (checkBankStatements)
        {
            var statements = await _bankStatementRepo.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
            foreach (var s in statements)
            {
                var label = $"{s.BankName} ({s.AccountNumber}) {s.PeriodStart:MMM yyyy}–{s.PeriodEnd:MMM yyyy}";
                if (request.Stage == "CreditAnalysis")
                {
                    // At credit analysis, the credit officer must have run cashflow analysis on every statement.
                    if (s.AnalysisStatus != AnalysisStatus.Completed)
                        pending.Add(new GateItem(s.Id, "BankStatement", label, "Pending", "Cashflow analysis not yet completed"));
                }
                else
                {
                    if (s.VerificationStatus == StatementVerificationStatus.Rejected)
                        rejected.Add(new GateItem(s.Id, "BankStatement", label, "Rejected", s.VerificationNotes));
                    else if (s.VerificationStatus == StatementVerificationStatus.Pending)
                        pending.Add(new GateItem(s.Id, "BankStatement", label, "Pending", null));
                }
            }
        }

        if (checkFinancials)
        {
            var financials = await _financialStatementRepo.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
            foreach (var fs in financials)
            {
                var label = $"{fs.FinancialYear} Financial Statement";
                if (fs.Status == FinancialStatementStatus.Rejected)
                    rejected.Add(new GateItem(fs.Id, "FinancialStatement", label, "Rejected", fs.RejectionReason));
                else if (fs.Status is FinancialStatementStatus.Draft or FinancialStatementStatus.PendingReview)
                    pending.Add(new GateItem(fs.Id, "FinancialStatement", label, "Pending", null));
            }
        }

        if (checkCollateral)
        {
            var collaterals = await _collateralRepo.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
            foreach (var c in collaterals)
            {
                var label = string.IsNullOrEmpty(c.Description) ? c.Type.ToString() : c.Description;
                if (c.Status == CollateralStatus.Rejected)
                    rejected.Add(new GateItem(c.Id, "Collateral", label, "Rejected", c.RejectionReason));
                else if (request.Stage == "CreditAnalysis")
                {
                    // Credit Officer's job is to enter valuation — Valued is done at this stage
                    if (c.Status is CollateralStatus.Proposed or CollateralStatus.UnderValuation)
                        pending.Add(new GateItem(c.Id, "Collateral", label, "Pending", "Valuation not yet entered"));
                }
                else
                {
                    // FinalApproval: collateral must be fully approved (legal clearance + credit sign-off)
                    if (c.Status is CollateralStatus.Proposed or CollateralStatus.UnderValuation or CollateralStatus.Valued)
                        pending.Add(new GateItem(c.Id, "Collateral", label, "Pending", null));
                }
            }
        }

        if (checkGuarantors)
        {
            var guarantors = await _guarantorRepo.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
            foreach (var g in guarantors)
            {
                if (g.Status == GuarantorStatus.Rejected)
                    rejected.Add(new GateItem(g.Id, "Guarantor", g.FullName, "Rejected", g.RejectionReason));
                else if (g.Status is GuarantorStatus.Proposed or GuarantorStatus.PendingVerification
                         or GuarantorStatus.CreditCheckPending or GuarantorStatus.CreditCheckCompleted)
                    pending.Add(new GateItem(g.Id, "Guarantor", g.FullName, "Pending", null));
            }
        }

        if (checkLegalClearance)
        {
            var collaterals = await _collateralRepo.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
            foreach (var c in collaterals)
            {
                if (c.Status == CollateralStatus.Rejected) continue; // already excluded from legal scope

                var label = string.IsNullOrEmpty(c.Description) ? c.Type.ToString() : c.Description;
                if (!c.IsLegalCleared)
                    pending.Add(new GateItem(c.Id, "Collateral", label, "Pending", "Legal clearance not yet recorded"));
            }
        }

        return ApplicationResult<ApprovalGateResult>.Success(new ApprovalGateResult(isStrict, rejected, pending));
    }
}
