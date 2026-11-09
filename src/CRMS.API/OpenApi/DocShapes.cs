namespace CRMS.API.OpenApi;

// Shapes that exist only for the API reference. Several actions answer with an anonymous object;
// these records mirror those objects field for field so the reference can show them. They are
// never constructed. If an action's anonymous payload changes, change its record here too.

/// <summary>The error body the partner-facing endpoints return.</summary>
/// <param name="Error">A stable machine-readable code, for example <c>unauthorized</c>,
/// <c>invalid_payload</c>, <c>validation_failed</c>, <c>rate_limited</c>, <c>not_found</c>.</param>
/// <param name="Message">What went wrong, in a sentence. Never repeats an identifier.</param>
/// <param name="Messages">For <c>validation_failed</c>: one entry per problem, naming the field.</param>
public record ApiError(string Error, string? Message, IReadOnlyList<string>? Messages);

/// <summary>Acknowledgement that credit checks were queued for a loan application.</summary>
public record CreditChecksQueued(string Message, Guid LoanApplicationId);

/// <summary>How many transactions were added to a statement.</summary>
public record TransactionsAdded(int AddedCount);

/// <summary>What the NAMP webhook answers once an application is staged.</summary>
/// <param name="ApplicationId">The CRMS staging record id.</param>
/// <param name="ApplicationNumber">The CRMS application number, once the application is recalled.</param>
public record NampWebhookAccepted(Guid ApplicationId, string? ApplicationNumber);

/// <summary>Why the NAMP webhook refused a payload.</summary>
public record NampWebhookValidationProblem(string Message, IReadOnlyList<string> Errors);

/// <summary>Result of the connectivity probe.</summary>
/// <param name="Timestamp">When the probes ran (UTC).</param>
/// <param name="Overall"><c>ok</c> when every probe passed, otherwise <c>degraded</c>.</param>
/// <param name="Probes">One entry per dependency probed.</param>
public record ConnectivityReport(DateTime Timestamp, string Overall, IReadOnlyList<ConnectivityProbe> Probes);

/// <summary>One dependency's probe.</summary>
/// <param name="Service">Database, S3, CoreBanking, Fineract or SmartComply.</param>
/// <param name="Status"><c>ok</c> or <c>error</c>.</param>
/// <param name="Detail">What was seen.</param>
public record ConnectivityProbe(string Service, string Status, string Detail);

/// <summary>Result of a development seeding action.</summary>
public record DevSeedResult(bool Success, string Message, DateTime Timestamp);

/// <summary>Row counts per table, for checking what a development database holds.</summary>
public record SeedStatusReport(DateTime Timestamp, SeedStatusSummary Summary, IReadOnlyDictionary<string, int> Tables);

/// <summary>Totals across the tables counted.</summary>
public record SeedStatusSummary(int TotalTables, int TablesWithData, int TablesEmpty, int TotalRows);
