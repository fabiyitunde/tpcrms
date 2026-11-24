namespace CRMS.Application.CustomerCheck;

public interface ICompanyCheckService
{
    /// <summary>Runs the company sections asked for, each with its own timeout; one section failing
    /// never blanks another. Read-only against every provider.</summary>
    Task<CompanyCheckOutcome> CheckAsync(CustomerCheckRequester requester, CompanyCheckQuery query, CancellationToken ct = default);
}
