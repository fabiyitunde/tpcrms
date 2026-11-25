using CRMS.Application.Access;
using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Access;
using CRMS.Web.Intranet.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRMS.Web.Intranet.Services;

/// <summary>
/// BOA's additional RH-SHF profiling requirements, staff side: the Credit Risk Readiness list, a
/// reviewer's verify/reject of each FAC document, the audited BVN/NIN reveal and the admin's
/// mandatory/optional switches. Every role or feature check is repeated in the handler; the page's
/// own checks only decide which buttons to show.
/// </summary>
public partial class ApplicationService
{
    public async Task<RhshfReadinessDto?> GetRhshfReadinessAsync(string reference)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetRhshfReadinessHandler>();
            var result = await handler.Handle(new GetRhshfReadinessQuery(reference), CancellationToken.None);
            return result.IsSuccess ? result.Data : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading RH-SHF readiness for {Reference}", reference);
            return null;
        }
    }

    public async Task<ApiResponse> ReviewRhshfSupportingDocumentAsync(string reference, Guid documentId, bool verified, string? reason, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<ReviewRhshfSupportingDocumentHandler>();
            var result = await handler.Handle(
                new ReviewRhshfSupportingDocumentCommand(reference, documentId, verified, reason, userId), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "The document could not be updated.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing RH-SHF document {DocumentId} on {Reference}", documentId, reference);
            return ApiResponse.Fail("The document could not be updated. Try again.");
        }
    }

    public async Task<ApiResponse<string>> RevealRhshfDirectorIdentityAsync(string reference, Guid directorId, bool nin, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<RevealRhshfDirectorIdentityHandler>();
            var result = await handler.Handle(new RevealRhshfDirectorIdentityQuery(reference, directorId, nin, userId), CancellationToken.None);
            return result.IsSuccess && result.Data is not null
                ? ApiResponse<string>.Ok(result.Data)
                : ApiResponse<string>.Fail(result.Error ?? "Could not reveal the number.");
        }
        catch (Exception ex)
        {
            // Includes a failed audit write: the reveal is refused rather than given unrecorded.
            _logger.LogError(ex, "Error revealing an RH-SHF director identifier on {Reference}", reference);
            return ApiResponse<string>.Fail("Could not reveal the number. Try again.");
        }
    }

    /// <summary>Whether the signed-in user holds a feature in Admin &gt; Feature Access. For showing
    /// buttons only — the handler behind each one checks again.</summary>
    public async Task<bool> IsFeatureAllowedAsync(Guid userId, string featureKey)
    {
        try
        {
            return await _sp.GetRequiredService<IFeatureAccessService>().IsAllowedAsync(userId, featureKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking feature {FeatureKey}", featureKey);
            return false;
        }
    }

    public Task<bool> CanViewRhshfPersonalDocumentsAsync(Guid userId) =>
        IsFeatureAllowedAsync(userId, FeatureKeys.RhshfPersonalDocumentsView);

    public Task<bool> CanRevealRhshfIdentityAsync(Guid userId) =>
        IsFeatureAllowedAsync(userId, FeatureKeys.RhshfIdentityReveal);

    public async Task<List<RhshfProfilingFieldRequirementDto>> GetRhshfProfilingFieldRequirementsAsync()
    {
        try
        {
            var handler = _sp.GetRequiredService<GetRhshfProfilingFieldRequirementsHandler>();
            var result = await handler.Handle(new GetRhshfProfilingFieldRequirementsQuery(), CancellationToken.None);
            return result.IsSuccess ? result.Data ?? [] : [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading RH-SHF profiling field requirements");
            return [];
        }
    }

    public async Task<ApiResponse> SetRhshfProfilingFieldRequirementAsync(Guid id, bool isMandatory, Guid userId, string userName)
    {
        try
        {
            var handler = _sp.GetRequiredService<SetRhshfProfilingFieldRequirementHandler>();
            var result = await handler.Handle(
                new SetRhshfProfilingFieldRequirementCommand(id, isMandatory, userId, userName), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Could not save the change.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating RH-SHF profiling field requirement {Id}", id);
            return ApiResponse.Fail("Could not save the change. Try again.");
        }
    }
}
