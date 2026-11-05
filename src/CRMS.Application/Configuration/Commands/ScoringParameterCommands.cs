using CRMS.Application.Common;
using CRMS.Application.Configuration.DTOs;
using CRMS.Domain.Aggregates.Configuration;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Configuration.Commands;

// Request a change to a scoring parameter (Maker step)
public record RequestParameterChangeCommand(
    Guid ParameterId,
    decimal NewValue,
    string Reason,
    Guid RequestedByUserId
) : IRequest<ApplicationResult<ScoringParameterDto>>;

public class RequestParameterChangeHandler : IRequestHandler<RequestParameterChangeCommand, ApplicationResult<ScoringParameterDto>>
{
    private readonly IScoringParameterRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RequestParameterChangeHandler(IScoringParameterRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<ScoringParameterDto>> Handle(RequestParameterChangeCommand request, CancellationToken ct = default)
    {
        var parameter = await _repository.GetByIdAsync(request.ParameterId, ct);
        if (parameter == null)
            return ApplicationResult<ScoringParameterDto>.Failure("Scoring parameter not found");

        var result = parameter.RequestChange(request.NewValue, request.RequestedByUserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult<ScoringParameterDto>.Failure(result.Error);

        _repository.Update(parameter);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<ScoringParameterDto>.Success(MapToDto(parameter));
    }

    private static ScoringParameterDto MapToDto(ScoringParameter p) => ScoringParameterMapper.ToDto(p);
}

// Approve a pending change (Checker step)
public record ApproveParameterChangeCommand(
    Guid ParameterId,
    Guid ApprovedByUserId,
    string? Notes
) : IRequest<ApplicationResult<ScoringParameterDto>>;

public class ApproveParameterChangeHandler : IRequestHandler<ApproveParameterChangeCommand, ApplicationResult<ScoringParameterDto>>
{
    private readonly IScoringParameterRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveParameterChangeHandler(IScoringParameterRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<ScoringParameterDto>> Handle(ApproveParameterChangeCommand request, CancellationToken ct = default)
    {
        var parameter = await _repository.GetByIdAsync(request.ParameterId, ct);
        if (parameter == null)
            return ApplicationResult<ScoringParameterDto>.Failure("Scoring parameter not found");

        // Store values before approval for history
        var previousValue = parameter.CurrentValue;
        var changeReason = parameter.PendingChangeReason;
        var requestedBy = parameter.PendingChangeByUserId;
        var requestedAt = parameter.PendingChangeAt;

        var result = parameter.ApproveChange(request.ApprovedByUserId, request.Notes);
        if (result.IsFailure)
            return ApplicationResult<ScoringParameterDto>.Failure(result.Error);

        // Record history
        var history = ScoringParameterHistory.RecordApprovedChange(
            parameter.Id,
            parameter.Category,
            parameter.ParameterKey,
            previousValue,
            parameter.CurrentValue,
            changeReason ?? "",
            requestedBy ?? Guid.Empty,
            requestedAt ?? DateTime.UtcNow,
            request.ApprovedByUserId,
            request.Notes,
            parameter.Version
        );
        await _repository.AddHistoryAsync(history, ct);

        _repository.Update(parameter);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<ScoringParameterDto>.Success(ScoringParameterMapper.ToDto(parameter));
    }
}

// Reject a pending change
public record RejectParameterChangeCommand(
    Guid ParameterId,
    Guid RejectedByUserId,
    string Reason
) : IRequest<ApplicationResult<ScoringParameterDto>>;

public class RejectParameterChangeHandler : IRequestHandler<RejectParameterChangeCommand, ApplicationResult<ScoringParameterDto>>
{
    private readonly IScoringParameterRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectParameterChangeHandler(IScoringParameterRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<ScoringParameterDto>> Handle(RejectParameterChangeCommand request, CancellationToken ct = default)
    {
        var parameter = await _repository.GetByIdAsync(request.ParameterId, ct);
        if (parameter == null)
            return ApplicationResult<ScoringParameterDto>.Failure("Scoring parameter not found");

        // Store values for history
        var requestedValue = parameter.PendingValue;
        var changeReason = parameter.PendingChangeReason;
        var requestedBy = parameter.PendingChangeByUserId;
        var requestedAt = parameter.PendingChangeAt;

        var result = parameter.RejectChange(request.RejectedByUserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult<ScoringParameterDto>.Failure(result.Error);

        // Record history
        var history = ScoringParameterHistory.RecordRejectedChange(
            parameter.Id,
            parameter.Category,
            parameter.ParameterKey,
            parameter.CurrentValue,
            requestedValue ?? 0,
            changeReason ?? "",
            requestedBy ?? Guid.Empty,
            requestedAt ?? DateTime.UtcNow,
            request.RejectedByUserId,
            request.Reason,
            parameter.Version
        );
        await _repository.AddHistoryAsync(history, ct);

        _repository.Update(parameter);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<ScoringParameterDto>.Success(ScoringParameterMapper.ToDto(parameter));
    }
}

// Cancel a pending change (by original requester)
public record CancelParameterChangeCommand(
    Guid ParameterId,
    Guid CancelledByUserId
) : IRequest<ApplicationResult<ScoringParameterDto>>;

public class CancelParameterChangeHandler : IRequestHandler<CancelParameterChangeCommand, ApplicationResult<ScoringParameterDto>>
{
    private readonly IScoringParameterRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelParameterChangeHandler(IScoringParameterRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<ScoringParameterDto>> Handle(CancelParameterChangeCommand request, CancellationToken ct = default)
    {
        var parameter = await _repository.GetByIdAsync(request.ParameterId, ct);
        if (parameter == null)
            return ApplicationResult<ScoringParameterDto>.Failure("Scoring parameter not found");

        var result = parameter.CancelChange(request.CancelledByUserId);
        if (result.IsFailure)
            return ApplicationResult<ScoringParameterDto>.Failure(result.Error);

        _repository.Update(parameter);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<ScoringParameterDto>.Success(ScoringParameterMapper.ToDto(parameter));
    }
}

// Seed default parameters (for initial setup)
public record SeedDefaultParametersCommand(Guid CreatedByUserId) : IRequest<ApplicationResult<int>>;

public class SeedDefaultParametersHandler : IRequestHandler<SeedDefaultParametersCommand, ApplicationResult<int>>
{
    private readonly IScoringParameterRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SeedDefaultParametersHandler(IScoringParameterRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<int>> Handle(SeedDefaultParametersCommand request, CancellationToken ct = default)
    {
        // Add-missing, never all-or-nothing: an environment seeded months ago still needs the
        // parameters added since, and a value an administrator has changed is never overwritten.
        var existing = await _repository.GetAllActiveAsync(ct);
        var present = existing.Select(p => $"{p.Category}.{p.ParameterKey}").ToHashSet(StringComparer.OrdinalIgnoreCase);

        var parameters = GetDefaultParameters(request.CreatedByUserId);
        var count = 0;

        foreach (var param in parameters)
        {
            if (param.IsSuccess && !present.Contains($"{param.Value.Category}.{param.Value.ParameterKey}"))
            {
                await _repository.AddAsync(param.Value, ct);
                
                var history = ScoringParameterHistory.RecordCreation(
                    param.Value.Id,
                    param.Value.Category,
                    param.Value.ParameterKey,
                    param.Value.CurrentValue,
                    request.CreatedByUserId
                );
                await _repository.AddHistoryAsync(history, ct);
                count++;
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult<int>.Success(count);
    }

    private static List<Result<ScoringParameter>> GetDefaultParameters(Guid userId)
    {
        var parameters = new List<Result<ScoringParameter>>();
        var sortOrder = 0;

        // Weights
        parameters.Add(ScoringParameter.Create("Weights", "CreditHistory", "Credit History Weight", "Weight for credit history category in overall score", ParameterDataType.Decimal, 0.25m, userId, 0, 1, sortOrder++));
        parameters.Add(ScoringParameter.Create("Weights", "FinancialHealth", "Financial Health Weight", "Weight for financial health category", ParameterDataType.Decimal, 0.25m, userId, 0, 1, sortOrder++));
        parameters.Add(ScoringParameter.Create("Weights", "CashflowStability", "Cashflow Stability Weight", "Weight for cashflow analysis category", ParameterDataType.Decimal, 0.15m, userId, 0, 1, sortOrder++));
        parameters.Add(ScoringParameter.Create("Weights", "DebtServiceCapacity", "Debt Service Capacity Weight", "Weight for DSCR category", ParameterDataType.Decimal, 0.20m, userId, 0, 1, sortOrder++));
        parameters.Add(ScoringParameter.Create("Weights", "CollateralCoverage", "Collateral Coverage Weight", "Weight for collateral category", ParameterDataType.Decimal, 0.15m, userId, 0, 1, sortOrder++));

        // Credit History
        sortOrder = 0;
        parameters.Add(ScoringParameter.Create("CreditHistory", "BaseScore", "Base Score", "Starting score before adjustments", ParameterDataType.Score, 70m, userId, 0, 100, sortOrder++));
        parameters.Add(ScoringParameter.Create("CreditHistory", "ExcellentCreditScoreThreshold", "Excellent Credit Score Threshold", "Credit score considered excellent", ParameterDataType.Integer, 700m, userId, 300, 850, sortOrder++));
        parameters.Add(ScoringParameter.Create("CreditHistory", "GoodCreditScoreThreshold", "Good Credit Score Threshold", "Credit score considered good", ParameterDataType.Integer, 650m, userId, 300, 850, sortOrder++));
        parameters.Add(ScoringParameter.Create("CreditHistory", "PoorCreditScoreThreshold", "Poor Credit Score Threshold", "Credit score considered poor", ParameterDataType.Integer, 600m, userId, 300, 850, sortOrder++));
        parameters.Add(ScoringParameter.Create("CreditHistory", "DefaultPenalty", "Default Penalty", "Score deduction for loan defaults", ParameterDataType.Score, 30m, userId, 0, 100, sortOrder++));
        parameters.Add(ScoringParameter.Create("CreditHistory", "DelinquencyPenalty", "Delinquency Penalty", "Score deduction for delinquent loans", ParameterDataType.Score, 15m, userId, 0, 100, sortOrder++));

        // Cashflow
        sortOrder = 0;
        parameters.Add(ScoringParameter.Create("Cashflow", "BaseScore", "Base Score", "Starting score for cashflow assessment", ParameterDataType.Score, 60m, userId, 0, 100, sortOrder++));
        parameters.Add(ScoringParameter.Create("Cashflow", "InternalStatementBonus", "Internal Statement Bonus", "Bonus for having internal bank statement", ParameterDataType.Score, 10m, userId, 0, 50, sortOrder++));
        parameters.Add(ScoringParameter.Create("Cashflow", "MissingInternalPenalty", "Missing Internal Penalty", "Penalty for missing internal statement", ParameterDataType.Score, 15m, userId, 0, 50, sortOrder++));
        parameters.Add(ScoringParameter.Create("Cashflow", "GamblingPenalty", "Gambling Penalty", "Penalty for gambling transactions", ParameterDataType.Score, 15m, userId, 0, 50, sortOrder++));
        parameters.Add(ScoringParameter.Create("Cashflow", "BouncedTransactionPenalty", "Bounced Transaction Penalty", "Penalty for bounced/failed transactions", ParameterDataType.Score, 20m, userId, 0, 50, sortOrder++));

        // Recommendations
        sortOrder = 0;
        parameters.Add(ScoringParameter.Create("Recommendations", "StrongApproveMinScore", "Strong Approve Min Score", "Minimum score for strong approval", ParameterDataType.Score, 75m, userId, 0, 100, sortOrder++));
        parameters.Add(ScoringParameter.Create("Recommendations", "ApproveMinScore", "Approve Min Score", "Minimum score for approval", ParameterDataType.Score, 65m, userId, 0, 100, sortOrder++));
        parameters.Add(ScoringParameter.Create("Recommendations", "ApproveWithConditionsMinScore", "Approve with Conditions Min Score", "Minimum score for conditional approval", ParameterDataType.Score, 50m, userId, 0, 100, sortOrder++));
        parameters.Add(ScoringParameter.Create("Recommendations", "ReferMinScore", "Refer Min Score", "Minimum score for referral (below = decline)", ParameterDataType.Score, 35m, userId, 0, 100, sortOrder++));
        parameters.Add(ScoringParameter.Create("Recommendations", "CriticalRedFlagsThreshold", "Critical Red Flags Threshold", "Number of red flags that trigger decline", ParameterDataType.Integer, 3m, userId, 1, 10, sortOrder++));

        // Loan Adjustments
        sortOrder = 0;
        parameters.Add(ScoringParameter.Create("LoanAdjustments", "BaseInterestRate", "Base Interest Rate", "Base interest rate percentage", ParameterDataType.Percentage, 18.0m, userId, 0, 50, sortOrder++));
        parameters.Add(ScoringParameter.Create("LoanAdjustments", "Score80PlusRateAdjustment", "Score 80+ Rate Adjustment", "Interest rate adjustment for scores 80+", ParameterDataType.Percentage, -2.0m, userId, -10, 10, sortOrder++));
        parameters.Add(ScoringParameter.Create("LoanAdjustments", "Score70PlusRateAdjustment", "Score 70+ Rate Adjustment", "Interest rate adjustment for scores 70-79", ParameterDataType.Percentage, -1.0m, userId, -10, 10, sortOrder++));
        parameters.Add(ScoringParameter.Create("LoanAdjustments", "MaxTenorForLowScores", "Max Tenor for Low Scores", "Maximum tenor (months) for low-scoring applications", ParameterDataType.Integer, 36m, userId, 6, 120, sortOrder++));

        // Customer check: the profile for scoring one individual by BVN (docs/Customer_Check.md)
        sortOrder = 0;
        parameters.Add(ScoringParameter.Create("CustomerCheck", "CreditHistoryWeight", "Credit History Weight", "Weight of the bureau and bank loan history in the individual's overall score", ParameterDataType.Decimal, 0.40m, userId, 0, 1, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "RepaymentCapacityWeight", "Repayment Capacity Weight", "Weight of monthly obligations against monthly inflow", ParameterDataType.Decimal, 0.25m, userId, 0, 1, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "CashflowStabilityWeight", "Cashflow Stability Weight", "Weight of statement activity and stability", ParameterDataType.Decimal, 0.20m, userId, 0, 1, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "IdentityAndFraudWeight", "Identity and Fraud Weight", "Weight of BVN verification, watch-list and fraud score", ParameterDataType.Decimal, 0.15m, userId, 0, 1, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "ComfortableObligationRatio", "Comfortable Obligation Ratio", "Obligations over inflow at or below this are comfortable", ParameterDataType.Decimal, 0.30m, userId, 0, 2, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "ManageableObligationRatio", "Manageable Obligation Ratio", "Obligations over inflow at or below this are manageable", ParameterDataType.Decimal, 0.45m, userId, 0, 2, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "StretchedObligationRatio", "Stretched Obligation Ratio", "Obligations over inflow at or below this are stretched", ParameterDataType.Decimal, 0.60m, userId, 0, 2, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "CriticalObligationRatio", "Critical Obligation Ratio", "Obligations over inflow above this are beyond critical", ParameterDataType.Decimal, 0.80m, userId, 0, 2, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "AssumedProposalPenalty", "Assumed Proposal Penalty", "Taken off repayment capacity when no loan amount and tenor were supplied", ParameterDataType.Score, 5m, userId, 0, 50, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "BankOverdueLoanPenalty", "Bank Overdue Loan Penalty", "Deduction when a loan with the bank is past due", ParameterDataType.Score, 15m, userId, 0, 50, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "BankWrittenOffPenalty", "Bank Written-Off Penalty", "Deduction when a loan with the bank was written off", ParameterDataType.Score, 30m, userId, 0, 50, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "BankPaidLoansBonus", "Bank Paid Loans Bonus", "Bonus for a track record of loans repaid to the bank", ParameterDataType.Score, 5m, userId, 0, 50, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "NoBureauRecordPenalty", "No Bureau Record Penalty", "Deduction when no bureau holds a record for the person", ParameterDataType.Score, 5m, userId, 0, 50, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "WatchListedPenalty", "Watch-Listed Penalty", "Deduction when the BVN is watch-listed", ParameterDataType.Score, 40m, userId, 0, 100, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "HighFraudRiskPenalty", "High Fraud Risk Penalty", "Deduction when the fraud check rates the person high risk", ParameterDataType.Score, 35m, userId, 0, 100, sortOrder++));
        parameters.Add(ScoringParameter.Create("CustomerCheck", "ElevatedFraudRiskPenalty", "Elevated Fraud Risk Penalty", "Deduction when the fraud check rates the person medium risk", ParameterDataType.Score, 15m, userId, 0, 100, sortOrder++));

        return parameters;
    }
}

// Mapper
internal static class ScoringParameterMapper
{
    public static ScoringParameterDto ToDto(ScoringParameter p) => new(
        p.Id,
        p.Category,
        p.ParameterKey,
        p.DisplayName,
        p.Description,
        p.DataType.ToString(),
        p.CurrentValue,
        p.MinValue,
        p.MaxValue,
        p.PendingValue,
        p.PendingChangeByUserId,
        p.PendingChangeAt,
        p.PendingChangeReason,
        p.ChangeStatus.ToString(),
        p.LastModifiedAt,
        p.Version,
        p.IsActive,
        p.SortOrder
    );
}
