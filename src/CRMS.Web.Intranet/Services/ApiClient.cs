using System.Net.Http.Json;
using CRMS.Web.Intranet.Models;

namespace CRMS.Web.Intranet.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<DashboardSummary>("api/reporting/dashboard");
            return response ?? new DashboardSummary();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching dashboard summary");
            return new DashboardSummary();
        }
    }

    public async Task<List<PendingTask>> GetMyPendingTasksAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<PendingTask>>("api/workflow/my-queue");
            return response ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending tasks");
            return [];
        }
    }

    public async Task<(List<LoanApplicationSummary> Items, int TotalCount)> GetApplicationsAsync(ApplicationFilter filter)
    {
        try
        {
            var query = BuildQueryString(filter);
            var response = await _httpClient.GetFromJsonAsync<PagedResult<LoanApplicationSummary>>($"api/loanapplications?{query}");
            return (response?.Items ?? [], response?.TotalCount ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching applications");
            return ([], 0);
        }
    }

    public async Task<LoanApplicationDetail?> GetApplicationDetailAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<LoanApplicationDetail>($"api/loanapplications/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching application detail");
            return null;
        }
    }

    public async Task<ApiResponse<Guid>> CreateApplicationAsync(CreateApplicationRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/loanapplications", request);
            if (response.IsSuccessStatusCode)
            {
                var id = await response.Content.ReadFromJsonAsync<Guid>();
                return ApiResponse<Guid>.Ok(id);
            }
            var error = await response.Content.ReadAsStringAsync();
            return ApiResponse<Guid>.Fail(error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating application");
            return ApiResponse<Guid>.Fail("Failed to create application");
        }
    }

    public async Task<ApiResponse> SubmitApplicationAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/loanapplications/{id}/submit", null);
            return response.IsSuccessStatusCode 
                ? ApiResponse.Ok() 
                : ApiResponse.Fail(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting application");
            return ApiResponse.Fail("Failed to submit application");
        }
    }

    public async Task<ApiResponse> ApproveApplicationAsync(Guid id, string comments)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/workflow/{id}/transition", 
                new { Action = "Approve", Comments = comments });
            return response.IsSuccessStatusCode 
                ? ApiResponse.Ok() 
                : ApiResponse.Fail(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving application");
            return ApiResponse.Fail("Failed to approve application");
        }
    }

    public async Task<ApiResponse> RejectApplicationAsync(Guid id, string comments)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/workflow/{id}/transition", 
                new { Action = "Reject", Comments = comments });
            return response.IsSuccessStatusCode 
                ? ApiResponse.Ok() 
                : ApiResponse.Fail(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting application");
            return ApiResponse.Fail("Failed to reject application");
        }
    }

    public async Task<ApiResponse> ReturnApplicationAsync(Guid id, string comments)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/workflow/{id}/transition", 
                new { Action = "Return", Comments = comments });
            return response.IsSuccessStatusCode 
                ? ApiResponse.Ok() 
                : ApiResponse.Fail(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning application");
            return ApiResponse.Fail("Failed to return application");
        }
    }

    public async Task<List<LoanProduct>> GetLoanProductsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<LoanProduct>>("api/loanproducts");
            return response ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching loan products");
            return [];
        }
    }

    public async Task<CustomerInfo?> FetchCustomerFromCoreBankingAsync(string accountNumber)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<CustomerInfo>($"api/corebanking/customer/{accountNumber}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching customer from core banking");
            return null;
        }
    }

    public async Task<ApiResponse> RequestBureauCheckAsync(Guid applicationId, Guid partyId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/creditbureau/request/{applicationId}/{partyId}", null);
            return response.IsSuccessStatusCode 
                ? ApiResponse.Ok() 
                : ApiResponse.Fail(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting bureau check");
            return ApiResponse.Fail("Failed to request bureau check");
        }
    }

    public async Task<ApiResponse> GenerateAdvisoryAsync(Guid applicationId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"api/advisory/generate/{applicationId}", null);
            return response.IsSuccessStatusCode 
                ? ApiResponse.Ok() 
                : ApiResponse.Fail(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating advisory");
            return ApiResponse.Fail("Failed to generate advisory");
        }
    }

    public async Task<byte[]?> GenerateLoanPackAsync(Guid applicationId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/loanpack/generate/{applicationId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating loan pack");
            return null;
        }
    }

    public async Task<ApiResponse> CastVoteAsync(Guid reviewId, string vote, string? comments)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/committee/{reviewId}/vote", 
                new { Vote = vote, Comments = comments });
            return response.IsSuccessStatusCode 
                ? ApiResponse.Ok() 
                : ApiResponse.Fail(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error casting vote");
            return ApiResponse.Fail("Failed to cast vote");
        }
    }

    public async Task<ApiResponse> AddCommentAsync(Guid applicationId, string content)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"api/loanapplications/{applicationId}/comments", 
                new { Content = content });
            return response.IsSuccessStatusCode 
                ? ApiResponse.Ok() 
                : ApiResponse.Fail(await response.Content.ReadAsStringAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment");
            return ApiResponse.Fail("Failed to add comment");
        }
    }

    private static string BuildQueryString(ApplicationFilter filter)
    {
        var parts = new List<string>();
        
        if (!string.IsNullOrEmpty(filter.Status))
            parts.Add($"status={Uri.EscapeDataString(filter.Status)}");
        if (!string.IsNullOrEmpty(filter.SearchTerm))
            parts.Add($"search={Uri.EscapeDataString(filter.SearchTerm)}");
        if (filter.DateFrom.HasValue)
            parts.Add($"dateFrom={filter.DateFrom.Value:yyyy-MM-dd}");
        if (filter.DateTo.HasValue)
            parts.Add($"dateTo={filter.DateTo.Value:yyyy-MM-dd}");
        if (filter.ProductId.HasValue)
            parts.Add($"productId={filter.ProductId.Value}");
        if (filter.AmountMin.HasValue)
            parts.Add($"amountMin={filter.AmountMin.Value}");
        if (filter.AmountMax.HasValue)
            parts.Add($"amountMax={filter.AmountMax.Value}");
        
        parts.Add($"page={filter.Page}");
        parts.Add($"pageSize={filter.PageSize}");
        
        return string.Join("&", parts);
    }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class ApiResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    public static ApiResponse Ok() => new() { Success = true };
    public static ApiResponse Fail(string error) => new() { Success = false, Error = error };
}

public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public new static ApiResponse<T> Fail(string error) => new() { Success = false, Error = error };
}

public class LoanProduct
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTenorMonths { get; set; }
    public int MaxTenorMonths { get; set; }
    public decimal BaseInterestRate { get; set; }
    public bool IsActive { get; set; }
}
