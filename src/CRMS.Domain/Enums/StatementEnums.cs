namespace CRMS.Domain.Enums;

public enum StatementFormat
{
    PDF,
    CSV,
    Excel,
    JSON
}

public enum StatementSource
{
    ManualUpload,
    CoreBanking,
    OpenBanking,
    MonoConnect
}

public enum TransactionCategory
{
    // Income Categories
    Salary,
    BusinessIncome,
    Investment,
    LoanInflow,
    TransferIn,
    Reversal,
    OtherIncome,

    // Expense Categories
    LoanRepayment,
    RentOrMortgage,
    Utilities,
    Gambling,
    Entertainment,
    TransferOut,
    BankCharges,
    CardPayment,
    CashWithdrawal,
    OtherExpense,

    // Neutral
    SelfTransfer,
    Unknown
}

public enum StatementTransactionType
{
    Credit,
    Debit
}

public enum AnalysisStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
