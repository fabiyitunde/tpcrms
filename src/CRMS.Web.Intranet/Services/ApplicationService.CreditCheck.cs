using CRMS.Application.Access;
using CRMS.Application.CreditCheck;
using CRMS.Web.Intranet.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRMS.Web.Intranet.Services;

/// <summary>
/// Credit checks staff run from the intranet, and Admin &gt; Feature Access. Every call here runs in
/// its own scope: the menu asks whether to show Credit Check while a page is loading, and the page
/// polls a running check, so sharing the circuit's DbContext would collide with whatever else the
/// circuit is doing.
/// </summary>
public partial class ApplicationService
{
    /// <summary>Whether the signed-in user may use a feature, read from the database (current roles
    /// and the saved rule), not from the roles cached at sign-in.</summary>
    public async Task<bool> CanUseFeatureAsync(string featureKey)
    {
        try
        {
            var userId = AuthUserId();
            if (userId is null) return false;
            using var scope = _sp.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IFeatureAccessService>().IsAllowedAsync(userId.Value, featureKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking access to {Feature}", featureKey);
            return false;
        }
    }

    public async Task<ApiResponse<Guid>> QueueStaffCreditCheckAsync(StaffCreditCheckRequest request)
    {
        try
        {
            var userId = AuthUserId();
            if (userId is null) return ApiResponse<Guid>.Fail("Sign in again to run a check.");
            using var scope = _sp.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<QueueStaffCreditCheckHandler>()
                .Handle(new QueueStaffCreditCheckCommand(userId.Value, request));
            return result.IsSuccess
                ? ApiResponse<Guid>.Ok(result.Data)
                : ApiResponse<Guid>.Fail(result.Errors is { Count: > 1 } errors ? string.Join(" ", errors) : result.Error ?? "The check could not be started.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error queueing a staff credit check");
            return ApiResponse<Guid>.Fail("The check could not be started.");
        }
    }

    public async Task<ApiResponse<StaffCreditCheckSummary>> GetStaffCreditCheckStatusAsync(Guid id)
    {
        try
        {
            var userId = AuthUserId();
            if (userId is null) return ApiResponse<StaffCreditCheckSummary>.Fail("Sign in again.");
            using var scope = _sp.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<GetStaffCreditCheckStatusHandler>()
                .Handle(new GetStaffCreditCheckStatusQuery(id, userId.Value));
            return result.IsSuccess ? ApiResponse<StaffCreditCheckSummary>.Ok(result.Data!) : ApiResponse<StaffCreditCheckSummary>.Fail(result.Error!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading staff credit check {Id}", id);
            return ApiResponse<StaffCreditCheckSummary>.Fail("The check could not be read.");
        }
    }

    public async Task<ApiResponse<StaffCreditCheckView>> GetStaffCreditCheckAsync(Guid id)
    {
        try
        {
            var userId = AuthUserId();
            if (userId is null) return ApiResponse<StaffCreditCheckView>.Fail("Sign in again.");
            using var scope = _sp.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<GetStaffCreditCheckHandler>()
                .Handle(new GetStaffCreditCheckQuery(id, userId.Value));
            return result.IsSuccess ? ApiResponse<StaffCreditCheckView>.Ok(result.Data!) : ApiResponse<StaffCreditCheckView>.Fail(result.Error!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening staff credit check {Id}", id);
            return ApiResponse<StaffCreditCheckView>.Fail("The check could not be opened.");
        }
    }

    public async Task<ApiResponse<StaffCreditCheckPage>> ListStaffCreditChecksAsync(
        bool everyone, string? subjectType, string? status, DateTime? from, DateTime? to, int page, int pageSize = 20)
    {
        try
        {
            var userId = AuthUserId();
            if (userId is null) return ApiResponse<StaffCreditCheckPage>.Fail("Sign in again.");
            using var scope = _sp.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<ListStaffCreditChecksHandler>()
                .Handle(new ListStaffCreditChecksQuery(userId.Value, everyone, subjectType, status, from, to, page, pageSize));
            return result.IsSuccess ? ApiResponse<StaffCreditCheckPage>.Ok(result.Data!) : ApiResponse<StaffCreditCheckPage>.Fail(result.Error!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing staff credit checks");
            return ApiResponse<StaffCreditCheckPage>.Fail("The checks could not be listed.");
        }
    }

    public async Task<ApiResponse<(byte[] Content, string FileName)>> DownloadStaffCreditCheckPdfAsync(Guid id)
    {
        try
        {
            var userId = AuthUserId();
            if (userId is null) return ApiResponse<(byte[], string)>.Fail("Sign in again.");
            using var scope = _sp.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<GetStaffCreditCheckPdfHandler>()
                .Handle(new GetStaffCreditCheckPdfQuery(id, userId.Value));
            return result.IsSuccess ? ApiResponse<(byte[], string)>.Ok(result.Data) : ApiResponse<(byte[], string)>.Fail(result.Error!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building the PDF for staff credit check {Id}", id);
            return ApiResponse<(byte[], string)>.Fail("The report could not be built.");
        }
    }

    // ── Admin > Feature Access ───────────────────────────────────────────────────────────────────

    public async Task<List<FeatureAccessRuleDto>> GetFeatureAccessRulesAsync()
    {
        try
        {
            using var scope = _sp.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<GetFeatureAccessRulesHandler>().Handle(new GetFeatureAccessRulesQuery());
            return result.Data ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading feature access rules");
            return [];
        }
    }

    public async Task<ApiResponse> UpdateFeatureAccessRuleAsync(string featureKey, List<string> roles)
    {
        try
        {
            var user = _sp.GetService<AuthService>()?.CurrentUser;
            if (user is null) return ApiResponse.Fail("Sign in again.");
            using var scope = _sp.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<UpdateFeatureAccessRuleHandler>()
                .Handle(new UpdateFeatureAccessRuleCommand(featureKey, roles, user.Id, user.FullName));
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "The change could not be saved.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving feature access rule {Feature}", featureKey);
            return ApiResponse.Fail("The change could not be saved.");
        }
    }
}
