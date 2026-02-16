using System.Text.Json;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using SA = CRMS.Domain.Aggregates.StatementAnalysis;

namespace CRMS.Domain.Services;

public class LLMTransactionCategorizationService
{
    private readonly ILLMService _llmService;
    private readonly TransactionCategorizationService _keywordService;
    private const int BatchSize = 50;

    public LLMTransactionCategorizationService(
        ILLMService llmService,
        TransactionCategorizationService keywordService)
    {
        _llmService = llmService;
        _keywordService = keywordService;
    }

    private static readonly string SystemPrompt = """
        You are a Nigerian bank statement transaction categorizer. 
        Analyze each transaction and categorize it into one of these categories:
        
        INCOME CATEGORIES:
        - Salary: Regular employment income (salary, wages, payroll)
        - BusinessIncome: Business revenue, sales, client payments
        - Investment: Dividends, interest income, investment returns
        - LoanInflow: Loan disbursements received
        - TransferIn: Money received from other accounts/people
        - Reversal: Refunds, reversed transactions
        - OtherIncome: Any other credit not fitting above
        
        EXPENSE CATEGORIES:
        - LoanRepayment: Loan EMI, mortgage payments, credit card payments
        - RentOrMortgage: Rent payments, housing costs
        - Utilities: Electricity (PHCN, EKEDC, IKEDC), water, internet, DSTV, airtime, data
        - Gambling: Betting sites (Bet9ja, SportyBet, BetKing, 1xBet, etc.) - RED FLAG
        - Entertainment: Netflix, Spotify, restaurants, cinema, leisure
        - TransferOut: Money sent to other accounts/people
        - BankCharges: Fees, COT, stamp duty, SMS alerts, maintenance
        - CardPayment: POS purchases, online payments
        - CashWithdrawal: ATM withdrawals, cash out
        - OtherExpense: Any other debit not fitting above
        
        NEUTRAL:
        - SelfTransfer: Transfer between own accounts
        - Unknown: Cannot determine
        
        For each transaction, provide:
        1. category: One of the categories above (exact spelling)
        2. confidence: 0.0 to 1.0 (how confident you are)
        3. reason: Brief explanation (max 20 words)
        
        Consider Nigerian context: PHCN/EKEDC/IKEDC are electricity, Bet9ja/SportyBet are gambling.
        """;

    public async Task CategorizeWithLLMAsync(SA.BankStatement statement, CancellationToken ct = default)
    {
        var transactions = statement.Transactions.ToList();
        
        // Process in batches to avoid token limits
        for (int i = 0; i < transactions.Count; i += BatchSize)
        {
            var batch = transactions.Skip(i).Take(BatchSize).ToList();
            await CategorizeBatchAsync(statement, batch, ct);
        }
    }

    private async Task CategorizeBatchAsync(SA.BankStatement statement, List<SA.StatementTransaction> batch, CancellationToken ct)
    {
        var transactionList = batch.Select((t, idx) => new
        {
            id = idx,
            date = t.Date.ToString("yyyy-MM-dd"),
            description = t.Description,
            amount = t.Amount,
            type = t.IsCredit ? "CREDIT" : "DEBIT"
        }).ToList();

        var transactionsJson = JsonSerializer.Serialize(transactionList, new JsonSerializerOptions { WriteIndented = true });
        var userPrompt = $"""
            Categorize these Nigerian bank transactions. Return JSON array only, no explanation.
            
            Transactions:
            {transactionsJson}
            
            Return format (example):
            [
              {"{"}"id": 0, "category": "Salary", "confidence": 0.95, "reason": "Monthly salary payment"{"}"},
              ...
            ]
            """;

        try
        {
            var response = await _llmService.CompleteAsJsonAsync<List<LLMCategorizationResult>>(SystemPrompt, userPrompt, ct);

            if (response != null)
            {
                foreach (var result in response)
                {
                    if (result.Id >= 0 && result.Id < batch.Count)
                    {
                        var transaction = batch[result.Id];
                        var category = ParseCategory(result.Category);
                        statement.CategorizeTransaction(transaction.Id, category, result.Confidence);
                    }
                }
            }
        }
        catch (Exception)
        {
            // Fallback to keyword-based categorization if LLM fails
            foreach (var transaction in batch)
            {
                var (category, confidence) = _keywordService.CategorizeTransaction(transaction);
                statement.CategorizeTransaction(transaction.Id, category, confidence);
            }
        }
    }

    private static TransactionCategory ParseCategory(string category)
    {
        return category?.Trim() switch
        {
            "Salary" => TransactionCategory.Salary,
            "BusinessIncome" => TransactionCategory.BusinessIncome,
            "Investment" => TransactionCategory.Investment,
            "LoanInflow" => TransactionCategory.LoanInflow,
            "TransferIn" => TransactionCategory.TransferIn,
            "Reversal" => TransactionCategory.Reversal,
            "OtherIncome" => TransactionCategory.OtherIncome,
            "LoanRepayment" => TransactionCategory.LoanRepayment,
            "RentOrMortgage" => TransactionCategory.RentOrMortgage,
            "Utilities" => TransactionCategory.Utilities,
            "Gambling" => TransactionCategory.Gambling,
            "Entertainment" => TransactionCategory.Entertainment,
            "TransferOut" => TransactionCategory.TransferOut,
            "BankCharges" => TransactionCategory.BankCharges,
            "CardPayment" => TransactionCategory.CardPayment,
            "CashWithdrawal" => TransactionCategory.CashWithdrawal,
            "OtherExpense" => TransactionCategory.OtherExpense,
            "SelfTransfer" => TransactionCategory.SelfTransfer,
            _ => TransactionCategory.Unknown
        };
    }
}

public class LLMCategorizationResult
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string? Reason { get; set; }
}
