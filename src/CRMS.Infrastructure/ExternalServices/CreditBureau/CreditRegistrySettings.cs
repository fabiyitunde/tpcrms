namespace CRMS.Infrastructure.ExternalServices.CreditBureau;

public class CreditRegistrySettings
{
    public const string SectionName = "CreditRegistry";
    
    public string BaseUrl { get; set; } = "https://api.creditregistry.com/nigeria/AutoCred/v8";
    public string SubscriberId { get; set; } = string.Empty;
    public string AgentUserId { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 60;
    public bool UseMock { get; set; } = true;
}
