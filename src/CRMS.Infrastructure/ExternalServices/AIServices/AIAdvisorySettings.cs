namespace CRMS.Infrastructure.ExternalServices.AIServices;

/// <summary>
/// Configuration settings for AI Advisory service.
/// </summary>
public class AIAdvisorySettings
{
    public const string SectionName = "AIAdvisory";

    /// <summary>
    /// Whether to use LLM for narrative generation.
    /// If false, uses template-based narratives only (rule-based).
    /// </summary>
    public bool UseLLMNarrative { get; set; } = false;

    /// <summary>
    /// Timeout in seconds for LLM API calls.
    /// </summary>
    public int LLMTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// If true, falls back to template narratives when LLM fails.
    /// If false, returns an error when LLM fails (not recommended).
    /// </summary>
    public bool FallbackToTemplateOnFailure { get; set; } = true;

    /// <summary>
    /// Whether to log the full LLM prompts and responses for debugging.
    /// Should be false in production for performance and cost reasons.
    /// </summary>
    public bool LogLLMInteractions { get; set; } = false;
}
