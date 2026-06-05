using CRMS.Domain.Interfaces;

namespace CRMS.Infrastructure.ExternalServices.AI;

/// <summary>
/// No-op LLM service used when no OpenAI API key is configured.
/// All calls return empty strings / null so that dependent services
/// (LLMNarrativeGenerator, HybridAIAdvisoryService) gracefully fall back
/// to their template-based paths without throwing a DI resolution error.
/// </summary>
public class NullLLMService : ILLMService
{
    public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        => Task.FromResult(string.Empty);

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        => Task.FromResult(string.Empty);

    public Task<T?> CompleteAsJsonAsync<T>(string prompt, CancellationToken ct = default) where T : class
        => Task.FromResult<T?>(null);

    public Task<T?> CompleteAsJsonAsync<T>(string systemPrompt, string userPrompt, CancellationToken ct = default) where T : class
        => Task.FromResult<T?>(null);
}
