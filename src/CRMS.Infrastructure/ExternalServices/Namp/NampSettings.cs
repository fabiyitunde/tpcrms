namespace CRMS.Infrastructure.ExternalServices.Namp;

public class NampSettings
{
    public const string SectionName = "Namp";

    public string CallbackUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public bool UseMock { get; set; } = true;
}
