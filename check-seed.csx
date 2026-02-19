// Quick script to check seeded data - run with: dotnet script check-seed.csx
#r "nuget: MySqlConnector, 2.3.1"

using MySqlConnector;

var connectionString = "Server=localhost;Database=crmsdb;User=devadmin;Password=1h[MUVA*hAIz<i3F;";

using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

var tables = new[] { "FinancialStatements", "LoanApplications", "Users", "WorkflowDefinitions", "BureauReports", "Collaterals", "Guarantors", "ConsentRecords", "BankStatements" };

foreach (var table in tables)
{
    using var cmd = new MySqlCommand($"SELECT COUNT(*) FROM {table}", connection);
    var count = await cmd.ExecuteScalarAsync();
    Console.WriteLine($"{table}: {count}");
}
