namespace CRMS.Infrastructure.ExternalServices.FineractDirect;

public class FineractDirectSettings
{
    public const string SectionName = "FineractDirect";

    public string BaseUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string TenantId { get; set; } = "default";
    public int TimeoutSeconds { get; set; } = 30;
    public bool UseMock { get; set; } = true;
}
