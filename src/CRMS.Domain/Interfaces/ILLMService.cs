namespace CRMS.Domain.Interfaces;

public interface ILLMService
{
    Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
    Task<T?> CompleteAsJsonAsync<T>(string prompt, CancellationToken ct = default) where T : class;
    Task<T?> CompleteAsJsonAsync<T>(string systemPrompt, string userPrompt, CancellationToken ct = default) where T : class;
}
