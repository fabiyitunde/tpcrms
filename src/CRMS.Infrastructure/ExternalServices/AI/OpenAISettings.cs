namespace CRMS.Infrastructure.ExternalServices.AI;

public class OpenAISettings
{
    public const string SectionName = "OpenAI";
    
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public string? BaseUrl { get; set; }
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.1;
    public int TimeoutSeconds { get; set; } = 60;
}
