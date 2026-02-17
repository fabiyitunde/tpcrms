using CRMS.Application.Common;
using CRMS.Application.FinancialAnalysis.DTOs;
using CRMS.Domain.Aggregates.FinancialStatement;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.FinancialAnalysis.Commands;

public record CreateFinancialStatementCommand(
    Guid LoanApplicationId,
    int FinancialYear,
    DateTime YearEndDate,
    FinancialYearType YearType,
    InputMethod InputMethod,
    Guid SubmittedByUserId,
    string Currency = "NGN",
    string? AuditorName = null,
    string? AuditorFirm = null,
    DateTime? AuditDate = null,
    string? AuditOpinion = null,
    string? OriginalFileName = null,
    string? FilePath = null
) : IRequest<ApplicationResult<FinancialStatementDto>>;

public class CreateFinancialStatementHandler : IRequestHandler<CreateFinancialStatementCommand, ApplicationResult<FinancialStatementDto>>
{
    private readonly IFinancialStatementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFinancialStatementHandler(IFinancialStatementRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<FinancialStatementDto>> Handle(CreateFinancialStatementCommand request, CancellationToken ct = default)
    {
        // Check if statement already exists for this year
        var existing = await _repository.GetByLoanApplicationAndYearAsync(request.LoanApplicationId, request.FinancialYear, ct);
        if (existing != null)
            return ApplicationResult<FinancialStatementDto>.Failure($"Financial statement for year {request.FinancialYear} already exists");

        var result = FinancialStatement.Create(
            request.LoanApplicationId,
            request.FinancialYear,
            request.YearEndDate,
            request.YearType,
            request.InputMethod,
            request.SubmittedByUserId,
            request.Currency,
            request.AuditorName,
            request.AuditorFirm,
            request.AuditDate,
            request.AuditOpinion,
            request.OriginalFileName,
            request.FilePath
        );

        if (result.IsFailure)
            return ApplicationResult<FinancialStatementDto>.Failure(result.Error);

        await _repository.AddAsync(result.Value, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<FinancialStatementDto>.Success(MapToDto(result.Value));
    }

    private static FinancialStatementDto MapToDto(FinancialStatement fs) => new(
        fs.Id, fs.LoanApplicationId, fs.FinancialYear, fs.YearEndDate,
        fs.YearType.ToString(), fs.Status.ToString(), fs.InputMethod.ToString(),
        fs.Currency, fs.AuditorName, fs.AuditorFirm, fs.AuditDate, fs.AuditOpinion,
        fs.OriginalFileName, fs.SubmittedAt, fs.VerifiedAt,
        null, null, null, null
    );
}

public record SetBalanceSheetCommand(
    Guid FinancialStatementId,
    SubmitBalanceSheetRequest Data
) : IRequest<ApplicationResult<FinancialStatementDto>>;

public class SetBalanceSheetHandler : IRequestHandler<SetBalanceSheetCommand, ApplicationResult<FinancialStatementDto>>
{
    private readonly IFinancialStatementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetBalanceSheetHandler(IFinancialStatementRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<FinancialStatementDto>> Handle(SetBalanceSheetCommand request, CancellationToken ct = default)
    {
        var statement = await _repository.GetByIdWithDetailsAsync(request.FinancialStatementId, ct);
        if (statement == null)
            return ApplicationResult<FinancialStatementDto>.Failure("Financial statement not found");

        var d = request.Data;
        var bs = BalanceSheet.Create(
            statement.Id,
            d.CashAndCashEquivalents, d.TradeReceivables, d.Inventory, d.PrepaidExpenses, d.OtherCurrentAssets,
            d.PropertyPlantEquipment, d.IntangibleAssets, d.LongTermInvestments, d.DeferredTaxAssets, d.OtherNonCurrentAssets,
            d.TradePayables, d.ShortTermBorrowings, d.CurrentPortionLongTermDebt, d.AccruedExpenses, d.TaxPayable, d.OtherCurrentLiabilities,
            d.LongTermDebt, d.DeferredTaxLiabilities, d.Provisions, d.OtherNonCurrentLiabilities,
            d.ShareCapital, d.SharePremium, d.RetainedEarnings, d.OtherReserves
        );

        var result = statement.SetBalanceSheet(bs);
        if (result.IsFailure)
            return ApplicationResult<FinancialStatementDto>.Failure(result.Error);

        _repository.Update(statement);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<FinancialStatementDto>.Success(MapToFullDto(statement));
    }

    private static FinancialStatementDto MapToFullDto(FinancialStatement fs) => FinancialStatementMapper.ToDto(fs);
}

public record SetIncomeStatementCommand(
    Guid FinancialStatementId,
    SubmitIncomeStatementRequest Data
) : IRequest<ApplicationResult<FinancialStatementDto>>;

public class SetIncomeStatementHandler : IRequestHandler<SetIncomeStatementCommand, ApplicationResult<FinancialStatementDto>>
{
    private readonly IFinancialStatementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetIncomeStatementHandler(IFinancialStatementRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<FinancialStatementDto>> Handle(SetIncomeStatementCommand request, CancellationToken ct = default)
    {
        var statement = await _repository.GetByIdWithDetailsAsync(request.FinancialStatementId, ct);
        if (statement == null)
            return ApplicationResult<FinancialStatementDto>.Failure("Financial statement not found");

        var d = request.Data;
        var inc = IncomeStatement.Create(
            statement.Id,
            d.Revenue, d.OtherOperatingIncome, d.CostOfSales,
            d.SellingExpenses, d.AdministrativeExpenses, d.DepreciationAmortization, d.OtherOperatingExpenses,
            d.InterestIncome, d.InterestExpense, d.OtherFinanceCosts,
            d.IncomeTaxExpense, d.DividendsDeclared
        );

        var result = statement.SetIncomeStatement(inc);
        if (result.IsFailure)
            return ApplicationResult<FinancialStatementDto>.Failure(result.Error);

        _repository.Update(statement);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<FinancialStatementDto>.Success(FinancialStatementMapper.ToDto(statement));
    }
}

public record SetCashFlowStatementCommand(
    Guid FinancialStatementId,
    SubmitCashFlowStatementRequest Data
) : IRequest<ApplicationResult<FinancialStatementDto>>;

public class SetCashFlowStatementHandler : IRequestHandler<SetCashFlowStatementCommand, ApplicationResult<FinancialStatementDto>>
{
    private readonly IFinancialStatementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetCashFlowStatementHandler(IFinancialStatementRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<FinancialStatementDto>> Handle(SetCashFlowStatementCommand request, CancellationToken ct = default)
    {
        var statement = await _repository.GetByIdWithDetailsAsync(request.FinancialStatementId, ct);
        if (statement == null)
            return ApplicationResult<FinancialStatementDto>.Failure("Financial statement not found");

        var d = request.Data;
        var cf = CashFlowStatement.Create(
            statement.Id,
            d.ProfitBeforeTax, d.DepreciationAmortization, d.InterestExpenseAddBack,
            d.ChangesInWorkingCapital, d.TaxPaid, d.OtherOperatingAdjustments,
            d.PurchaseOfPPE, d.SaleOfPPE, d.PurchaseOfInvestments, d.SaleOfInvestments,
            d.InterestReceived, d.DividendsReceived, d.OtherInvestingActivities,
            d.ProceedsFromBorrowings, d.RepaymentOfBorrowings, d.InterestPaid,
            d.DividendsPaid, d.ProceedsFromShareIssue, d.OtherFinancingActivities,
            d.OpeningCashBalance
        );

        var result = statement.SetCashFlowStatement(cf);
        if (result.IsFailure)
            return ApplicationResult<FinancialStatementDto>.Failure(result.Error);

        _repository.Update(statement);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<FinancialStatementDto>.Success(FinancialStatementMapper.ToDto(statement));
    }
}

public record SubmitFinancialStatementCommand(Guid FinancialStatementId) : IRequest<ApplicationResult<FinancialStatementDto>>;

public class SubmitFinancialStatementHandler : IRequestHandler<SubmitFinancialStatementCommand, ApplicationResult<FinancialStatementDto>>
{
    private readonly IFinancialStatementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SubmitFinancialStatementHandler(IFinancialStatementRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<FinancialStatementDto>> Handle(SubmitFinancialStatementCommand request, CancellationToken ct = default)
    {
        var statement = await _repository.GetByIdWithDetailsAsync(request.FinancialStatementId, ct);
        if (statement == null)
            return ApplicationResult<FinancialStatementDto>.Failure("Financial statement not found");

        var result = statement.Submit();
        if (result.IsFailure)
            return ApplicationResult<FinancialStatementDto>.Failure(result.Error);

        _repository.Update(statement);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<FinancialStatementDto>.Success(FinancialStatementMapper.ToDto(statement));
    }
}

public record VerifyFinancialStatementCommand(Guid FinancialStatementId, Guid VerifiedByUserId, string? Notes = null) 
    : IRequest<ApplicationResult<FinancialStatementDto>>;

public class VerifyFinancialStatementHandler : IRequestHandler<VerifyFinancialStatementCommand, ApplicationResult<FinancialStatementDto>>
{
    private readonly IFinancialStatementRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyFinancialStatementHandler(IFinancialStatementRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<FinancialStatementDto>> Handle(VerifyFinancialStatementCommand request, CancellationToken ct = default)
    {
        var statement = await _repository.GetByIdWithDetailsAsync(request.FinancialStatementId, ct);
        if (statement == null)
            return ApplicationResult<FinancialStatementDto>.Failure("Financial statement not found");

        var result = statement.Verify(request.VerifiedByUserId, request.Notes);
        if (result.IsFailure)
            return ApplicationResult<FinancialStatementDto>.Failure(result.Error);

        _repository.Update(statement);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<FinancialStatementDto>.Success(FinancialStatementMapper.ToDto(statement));
    }
}

// Mapper utility
internal static class FinancialStatementMapper
{
    public static FinancialStatementDto ToDto(FinancialStatement fs)
    {
        return new FinancialStatementDto(
            fs.Id, fs.LoanApplicationId, fs.FinancialYear, fs.YearEndDate,
            fs.YearType.ToString(), fs.Status.ToString(), fs.InputMethod.ToString(),
            fs.Currency, fs.AuditorName, fs.AuditorFirm, fs.AuditDate, fs.AuditOpinion,
            fs.OriginalFileName, fs.SubmittedAt, fs.VerifiedAt,
            fs.BalanceSheet != null ? MapBalanceSheet(fs.BalanceSheet) : null,
            fs.IncomeStatement != null ? MapIncomeStatement(fs.IncomeStatement) : null,
            fs.CashFlowStatement != null ? MapCashFlow(fs.CashFlowStatement) : null,
            fs.CalculatedRatios != null ? MapRatios(fs.CalculatedRatios) : null
        );
    }

    private static BalanceSheetDto MapBalanceSheet(BalanceSheet bs) => new(
        bs.CashAndCashEquivalents, bs.TradeReceivables, bs.Inventory, bs.PrepaidExpenses, bs.OtherCurrentAssets, bs.TotalCurrentAssets,
        bs.PropertyPlantEquipment, bs.IntangibleAssets, bs.LongTermInvestments, bs.DeferredTaxAssets, bs.OtherNonCurrentAssets, bs.TotalNonCurrentAssets, bs.TotalAssets,
        bs.TradePayables, bs.ShortTermBorrowings, bs.CurrentPortionLongTermDebt, bs.AccruedExpenses, bs.TaxPayable, bs.OtherCurrentLiabilities, bs.TotalCurrentLiabilities,
        bs.LongTermDebt, bs.DeferredTaxLiabilities, bs.Provisions, bs.OtherNonCurrentLiabilities, bs.TotalNonCurrentLiabilities, bs.TotalLiabilities,
        bs.ShareCapital, bs.SharePremium, bs.RetainedEarnings, bs.OtherReserves, bs.TotalEquity,
        bs.TotalDebt, bs.WorkingCapital, bs.NetWorth, bs.IsBalanced()
    );

    private static IncomeStatementDto MapIncomeStatement(IncomeStatement inc) => new(
        inc.Revenue, inc.OtherOperatingIncome, inc.TotalRevenue, inc.CostOfSales,
        inc.GrossProfit, inc.GrossMarginPercent,
        inc.SellingExpenses, inc.AdministrativeExpenses, inc.DepreciationAmortization, inc.OtherOperatingExpenses, inc.TotalOperatingExpenses,
        inc.OperatingProfit, inc.EBITDA, inc.EBITDAMarginPercent,
        inc.InterestIncome, inc.InterestExpense, inc.OtherFinanceCosts, inc.NetFinanceCost,
        inc.ProfitBeforeTax, inc.IncomeTaxExpense, inc.NetProfit, inc.NetProfitMarginPercent,
        inc.DividendsDeclared, inc.RetainedProfit, inc.IsProfitable
    );

    private static CashFlowStatementDto MapCashFlow(CashFlowStatement cf) => new(
        cf.ProfitBeforeTax, cf.DepreciationAmortization, cf.InterestExpenseAddBack,
        cf.ChangesInWorkingCapital, cf.TaxPaid, cf.OtherOperatingAdjustments, cf.NetCashFromOperations,
        cf.PurchaseOfPPE, cf.SaleOfPPE, cf.NetCashFromInvesting,
        cf.ProceedsFromBorrowings, cf.RepaymentOfBorrowings, cf.InterestPaid, cf.DividendsPaid, cf.NetCashFromFinancing,
        cf.NetChangeInCash, cf.OpeningCashBalance, cf.ClosingCashBalance,
        cf.FreeCashFlow, cf.HasPositiveOperatingCashFlow
    );

    private static FinancialRatiosDto MapRatios(FinancialRatios r) => new(
        r.CurrentRatio, r.QuickRatio, r.CashRatio, r.GetLiquidityAssessment(),
        r.DebtToEquityRatio, r.DebtToAssetsRatio, r.InterestCoverageRatio, r.DebtServiceCoverageRatio, r.GetLeverageAssessment(),
        r.GrossMarginPercent, r.OperatingMarginPercent, r.NetProfitMarginPercent, r.EBITDAMarginPercent,
        r.ReturnOnAssets, r.ReturnOnEquity, r.GetProfitabilityAssessment(),
        r.AssetTurnover, r.InventoryTurnover, r.ReceivablesDays, r.PayablesDays, r.CashConversionCycle,
        r.WorkingCapital, r.NetWorth, r.TotalDebt, r.GetOverallAssessment()
    );
}
