namespace CRMS.Infrastructure.ExternalServices.CoreBanking;

public class FineractSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string TenantId { get; set; } = "default";
    public string Username { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int RetryCount { get; set; } = 3;
    public bool UseMock { get; set; } = true;
}
