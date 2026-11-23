using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;

namespace CRMS.Web.Intranet.Services;

/// <summary>Bridges the shared, product-agnostic <see cref="FinancialStatementExcelService"/> (template
/// generation + Excel parsing) to RH-SHF's flat figures carrier, so RH-SHF reuses the exact same
/// template and parser as Corporate without its own Excel code. Auditor fields aren't in the template,
/// so they stay null here and are entered manually if needed.</summary>
public static class RhshfFinancialStatementMapper
{
    /// <summary>What an import did, in the officer's words. Verified years are named because the
    /// officer has to verify the new figures again.</summary>
    public static string DescribeImport(CRMS.Application.Rhshf.Commands.RhshfFinancialStatementsReplaced r, string? fileName)
    {
        var source = string.IsNullOrWhiteSpace(fileName) ? "the file" : $"\"{fileName}\"";
        var text = r.Replaced > 0
            ? $"Imported {r.Imported} year(s) from {source}, replacing the {r.Replaced} statement(s) already on the case."
            : $"Imported {r.Imported} year(s) from {source}.";
        if (r.VerifiedYearsReplaced.Count > 0)
            text += $" {string.Join(", ", r.VerifiedYearsReplaced)} had been verified; verify the new figures.";
        return text;
    }

    public static RhshfFinancialStatementFigures ToFigures(FinancialStatementUploadData d)
    {
        var bs = d.BalanceSheet;
        var inc = d.IncomeStatement;
        var cf = d.CashFlow;

        return new RhshfFinancialStatementFigures(
            inc.Revenue, inc.OtherOperatingIncome, inc.CostOfSales, inc.SellingExpenses, inc.AdministrativeExpenses,
            inc.DepreciationAmortization, inc.OtherOperatingExpenses, inc.InterestIncome, inc.InterestExpense,
            inc.OtherFinanceCosts, inc.IncomeTaxExpense, inc.DividendsDeclared,
            bs.CashAndCashEquivalents, bs.TradeReceivables, bs.Inventory, bs.PrepaidExpenses, bs.OtherCurrentAssets,
            bs.PropertyPlantEquipment, bs.IntangibleAssets, bs.LongTermInvestments, bs.DeferredTaxAssets, bs.OtherNonCurrentAssets,
            bs.TradePayables, bs.ShortTermBorrowings, bs.CurrentPortionLongTermDebt, bs.AccruedExpenses, bs.TaxPayable,
            bs.OtherCurrentLiabilities, bs.LongTermDebt, bs.DeferredTaxLiabilities, bs.Provisions, bs.OtherNonCurrentLiabilities,
            bs.ShareCapital, bs.SharePremium, bs.RetainedEarnings, bs.OtherReserves,
            cf?.ProfitBeforeTax, cf?.DepreciationAmortization, cf?.InterestExpenseAddBack, cf?.ChangesInWorkingCapital,
            cf?.TaxPaid, cf?.OtherOperatingAdjustments, cf?.PurchaseOfPPE, cf?.SaleOfPPE, cf?.PurchaseOfInvestments,
            cf?.SaleOfInvestments, cf?.InterestReceived, cf?.DividendsReceived, cf?.OtherInvestingActivities,
            cf?.ProceedsFromBorrowings, cf?.RepaymentOfBorrowings, cf?.InterestPaid, cf?.DividendsPaid,
            cf?.ProceedsFromShareIssue, cf?.OtherFinancingActivities, cf?.OpeningCashBalance,
            AuditorName: null, AuditorFirm: null, AuditDate: null, AuditOpinion: null);
    }

    public static RhshfFinancialYearType ParseYearType(string? yearType) =>
        Enum.TryParse<RhshfFinancialYearType>(yearType, ignoreCase: true, out var t) ? t : RhshfFinancialYearType.Audited;
}
