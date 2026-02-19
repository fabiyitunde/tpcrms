// Simple console app to count rows
using MySqlConnector;

var connectionString = "Server=localhost;Database=crmsdb;User=devadmin;Pwd=1h[MUVA*hAIz<i3F;";

try
{
    using var conn = new MySqlConnection(connectionString);
    await conn.OpenAsync();
    
    var tables = new[] {
        "Roles", "LoanProducts", "PricingTiers", "Users", "UserRoles",
        "Permissions", "RolePermissions", "WorkflowDefinitions", "WorkflowStages",
        "WorkflowTransitions", "NotificationTemplates", "ScoringParameters", "ScoringParameterHistory",
        "EligibilityRules", "DocumentRequirements", "LoanApplications", "LoanApplicationParties",
        "ConsentRecords", "BureauReports", "BureauAccounts", "BureauScoreFactors",
        "BankStatements", "StatementTransactions", "FinancialStatements", "Collaterals",
        "CollateralValuations", "CollateralDocuments", "Guarantors", "GuarantorDocuments",
        "CreditAdvisories", "CommitteeReviews", "CommitteeMembers",
        "CommitteeComments", "CommitteeDocuments", "LoanPacks", "WorkflowInstances",
        "WorkflowTransitionLogs", "AuditLogs", "DataAccessLogs", "Notifications"
    };
    
    Console.WriteLine("Table".PadRight(30) + "| Count");
    Console.WriteLine(new string('-', 45));
    
    int total = 0;
    int withData = 0;
    
    foreach (var table in tables)
    {
        try
        {
            using var cmd = new MySqlCommand($"SELECT COUNT(*) FROM `{table}`", conn);
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Console.WriteLine($"{table,-30}| {count}");
            total += count;
            if (count > 0) withData++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{table,-30}| ERROR");
        }
    }
    
    Console.WriteLine(new string('-', 45));
    Console.WriteLine($"Total rows: {total}");
    Console.WriteLine($"Tables with data: {withData}/{tables.Length}");
}
catch (Exception ex)
{
    Console.WriteLine($"Connection error: {ex.Message}");
}
