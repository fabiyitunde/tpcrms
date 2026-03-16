namespace CRMS.Infrastructure.ExternalServices.CoreBanking;

public class CoreBankingSettings
{
    public const string SectionName = "CoreBanking";

    public string BaseUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = "/oauth/token";
    public int TimeoutSeconds { get; set; } = 30;
    public bool UseMock { get; set; } = true;
}
