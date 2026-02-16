using CRMS.Domain.Aggregates.StatementAnalysis;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Services;

public class TransactionCategorizationService
{
    private static readonly Dictionary<string, (TransactionCategory Category, decimal Confidence)> KeywordMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Salary patterns
        { "SALARY", (TransactionCategory.Salary, 0.95m) },
        { "SAL/", (TransactionCategory.Salary, 0.90m) },
        { "PAYROLL", (TransactionCategory.Salary, 0.95m) },
        { "WAGES", (TransactionCategory.Salary, 0.90m) },
        { "MONTHLY PAY", (TransactionCategory.Salary, 0.85m) },

        // Loan patterns
        { "LOAN REPAY", (TransactionCategory.LoanRepayment, 0.95m) },
        { "LOAN PMT", (TransactionCategory.LoanRepayment, 0.90m) },
        { "EMI", (TransactionCategory.LoanRepayment, 0.85m) },
        { "MORTGAGE", (TransactionCategory.LoanRepayment, 0.90m) },
        { "CREDIT CARD PAYMENT", (TransactionCategory.LoanRepayment, 0.85m) },

        // Loan inflow
        { "LOAN DISBURSEMENT", (TransactionCategory.LoanInflow, 0.95m) },
        { "LOAN CREDIT", (TransactionCategory.LoanInflow, 0.90m) },

        // Rent
        { "RENT", (TransactionCategory.RentOrMortgage, 0.90m) },
        { "HOUSE RENT", (TransactionCategory.RentOrMortgage, 0.95m) },
        { "LANDLORD", (TransactionCategory.RentOrMortgage, 0.85m) },

        // Utilities
        { "ELECTRIC", (TransactionCategory.Utilities, 0.90m) },
        { "PHCN", (TransactionCategory.Utilities, 0.95m) },
        { "EKEDC", (TransactionCategory.Utilities, 0.95m) },
        { "IKEDC", (TransactionCategory.Utilities, 0.95m) },
        { "DSTV", (TransactionCategory.Utilities, 0.90m) },
        { "GOTV", (TransactionCategory.Utilities, 0.90m) },
        { "WATER BILL", (TransactionCategory.Utilities, 0.90m) },
        { "INTERNET", (TransactionCategory.Utilities, 0.85m) },
        { "AIRTIME", (TransactionCategory.Utilities, 0.80m) },
        { "DATA BUNDLE", (TransactionCategory.Utilities, 0.80m) },

        // Gambling (red flag)
        { "BET9JA", (TransactionCategory.Gambling, 0.99m) },
        { "SPORTYBET", (TransactionCategory.Gambling, 0.99m) },
        { "BETWAY", (TransactionCategory.Gambling, 0.99m) },
        { "1XBET", (TransactionCategory.Gambling, 0.99m) },
        { "NAIRABET", (TransactionCategory.Gambling, 0.99m) },
        { "BETKING", (TransactionCategory.Gambling, 0.99m) },
        { "MERRYBET", (TransactionCategory.Gambling, 0.99m) },
        { "BETTING", (TransactionCategory.Gambling, 0.90m) },
        { "CASINO", (TransactionCategory.Gambling, 0.95m) },

        // Bank charges
        { "CHARGE", (TransactionCategory.BankCharges, 0.80m) },
        { "FEE", (TransactionCategory.BankCharges, 0.75m) },
        { "COT", (TransactionCategory.BankCharges, 0.85m) },
        { "COMMISSION", (TransactionCategory.BankCharges, 0.80m) },
        { "SMS ALERT", (TransactionCategory.BankCharges, 0.90m) },
        { "MAINTENANCE FEE", (TransactionCategory.BankCharges, 0.90m) },
        { "STAMP DUTY", (TransactionCategory.BankCharges, 0.95m) },

        // ATM/Cash
        { "ATM", (TransactionCategory.CashWithdrawal, 0.90m) },
        { "CASH WITHDRAWAL", (TransactionCategory.CashWithdrawal, 0.95m) },
        { "POS WITHDRAWAL", (TransactionCategory.CashWithdrawal, 0.85m) },

        // Card payments
        { "POS", (TransactionCategory.CardPayment, 0.80m) },
        { "WEB PAYMENT", (TransactionCategory.CardPayment, 0.85m) },
        { "ONLINE PAYMENT", (TransactionCategory.CardPayment, 0.85m) },

        // Transfers
        { "NIP/", (TransactionCategory.TransferOut, 0.70m) },
        { "TRANSFER TO", (TransactionCategory.TransferOut, 0.85m) },
        { "TRF TO", (TransactionCategory.TransferOut, 0.85m) },
        { "TRANSFER FROM", (TransactionCategory.TransferIn, 0.85m) },
        { "TRF FROM", (TransactionCategory.TransferIn, 0.85m) },
        { "INWARD", (TransactionCategory.TransferIn, 0.80m) },

        // Reversal
        { "REVERSAL", (TransactionCategory.Reversal, 0.95m) },
        { "REFUND", (TransactionCategory.Reversal, 0.90m) },

        // Business
        { "SALES", (TransactionCategory.BusinessIncome, 0.75m) },
        { "INVOICE", (TransactionCategory.BusinessIncome, 0.80m) },
        { "PAYMENT RECEIVED", (TransactionCategory.BusinessIncome, 0.80m) },

        // Entertainment
        { "NETFLIX", (TransactionCategory.Entertainment, 0.95m) },
        { "SPOTIFY", (TransactionCategory.Entertainment, 0.95m) },
        { "CINEMA", (TransactionCategory.Entertainment, 0.90m) },
        { "RESTAURANT", (TransactionCategory.Entertainment, 0.80m) },
    };

    public (TransactionCategory Category, decimal Confidence) CategorizeTransaction(StatementTransaction transaction)
    {
        var description = transaction.NormalizedDescription;

        // First pass: exact keyword matching
        foreach (var (keyword, result) in KeywordMappings)
        {
            if (description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                // Adjust category based on transaction type
                var category = result.Category;
                
                // Handle transfer direction based on transaction type
                if (category == TransactionCategory.TransferOut && transaction.IsCredit)
                    category = TransactionCategory.TransferIn;
                else if (category == TransactionCategory.TransferIn && transaction.IsDebit)
                    category = TransactionCategory.TransferOut;

                return (category, result.Confidence);
            }
        }

        // Default categorization based on transaction type
        return transaction.IsCredit
            ? (TransactionCategory.OtherIncome, 0.3m)
            : (TransactionCategory.OtherExpense, 0.3m);
    }

    public void CategorizeAllTransactions(BankStatement statement)
    {
        foreach (var transaction in statement.Transactions)
        {
            var (category, confidence) = CategorizeTransaction(transaction);
            statement.CategorizeTransaction(transaction.Id, category, confidence);
        }
    }
}
