using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.ExternalServices.AI;

public class OpenAIService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAISettings _settings;
    private readonly ILogger<OpenAIService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpenAIService(
        HttpClient httpClient,
        IOptions<OpenAISettings> settings,
        ILogger<OpenAIService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        var baseUrl = _settings.BaseUrl ?? "https://api.openai.com/v1/";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        return await CompleteAsync("You are a helpful assistant.", prompt, ct);
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var request = new OpenAIRequest
        {
            Model = _settings.Model,
            MaxTokens = _settings.MaxTokens,
            Temperature = _settings.Temperature,
            Messages =
            [
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            ]
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("chat/completions", content, ct);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<OpenAIResponse>(responseJson, _jsonOptions);

            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI API call failed");
            throw;
        }
    }

    public async Task<T?> CompleteAsJsonAsync<T>(string prompt, CancellationToken ct = default) where T : class
    {
        return await CompleteAsJsonAsync<T>("You are a helpful assistant that responds in JSON.", prompt, ct);
    }

    public async Task<T?> CompleteAsJsonAsync<T>(string systemPrompt, string userPrompt, CancellationToken ct = default) where T : class
    {
        var response = await CompleteAsync(systemPrompt, userPrompt, ct);
        
        // Extract JSON from response (handle markdown code blocks)
        var jsonContent = ExtractJson(response);
        
        if (string.IsNullOrWhiteSpace(jsonContent))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(jsonContent, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM response as JSON: {Response}", response);
            return default;
        }
    }

    private static string ExtractJson(string response)
    {
        var trimmed = response.Trim();
        
        // Handle markdown code blocks
        if (trimmed.StartsWith("```json"))
        {
            var endIndex = trimmed.LastIndexOf("```");
            if (endIndex > 7)
                return trimmed[7..endIndex].Trim();
        }
        else if (trimmed.StartsWith("```"))
        {
            var endIndex = trimmed.LastIndexOf("```");
            if (endIndex > 3)
                return trimmed[3..endIndex].Trim();
        }
        
        // Try to find JSON array or object
        var startBracket = trimmed.IndexOf('[');
        var startBrace = trimmed.IndexOf('{');
        
        if (startBracket >= 0 && (startBrace < 0 || startBracket < startBrace))
        {
            var endBracket = trimmed.LastIndexOf(']');
            if (endBracket > startBracket)
                return trimmed[startBracket..(endBracket + 1)];
        }
        else if (startBrace >= 0)
        {
            var endBrace = trimmed.LastIndexOf('}');
            if (endBrace > startBrace)
                return trimmed[startBrace..(endBrace + 1)];
        }

        return trimmed;
    }
}

// OpenAI API DTOs
public class OpenAIRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("messages")]
    public List<OpenAIMessage> Messages { get; set; } = [];
    
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }
    
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }
}

public class OpenAIMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class OpenAIResponse
{
    [JsonPropertyName("choices")]
    public List<OpenAIChoice>? Choices { get; set; }
}

public class OpenAIChoice
{
    [JsonPropertyName("message")]
    public OpenAIMessage? Message { get; set; }
}
