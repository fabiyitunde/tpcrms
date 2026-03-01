namespace CRMS.Infrastructure.ExternalServices.SmartComply;

public class SmartComplySettings
{
    public const string SectionName = "SmartComply";
    
    public string BaseUrl { get; set; } = "https://adhere-api.smartcomply.com";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 60;
    public bool UseMock { get; set; } = true;
}
