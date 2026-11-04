using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Services;

/// <summary>
/// Checks the reason code staff picked when returning or declining a case (CRMSQA#1). The list is
/// the administrator's, so the check is against what is configured now: the code must exist, be
/// switched on, and be of the right type. OTHER is accepted for either type.
/// </summary>
public static class RhshfReasonCodeRules
{
    /// <summary>
    /// The code to record. None picked means OTHER, so a caller that does not send one (an older
    /// client, a script) still records a reason rather than failing; the staff dialogs require one.
    /// <paramref name="repo"/> is null only where a handler is built without the lookup, as in older
    /// tests; the code is then recorded as given, unchecked.
    /// </summary>
    public static async Task<Result<string>> ResolveAsync(
        IRhshfReasonCodeRepository? repo, string? code, RhshfReasonCodeType type, CancellationToken ct = default)
    {
        var picked = string.IsNullOrWhiteSpace(code) ? RhshfReasonCode.Other : code.Trim().ToUpperInvariant();
        if (repo is null)
            return Result.Success(picked);

        var configured = await repo.GetByCodeAsync(picked, ct);
        if (configured is null)
            return Result.Failure<string>($"\"{picked}\" is not a configured reason. Pick one from the list.");
        if (!configured.IsActive)
            return Result.Failure<string>($"The reason \"{configured.Label}\" has been switched off. Pick another.");
        if (configured.Type != type && configured.Code != RhshfReasonCode.Other)
            return Result.Failure<string>(type == RhshfReasonCodeType.Decline
                ? $"\"{configured.Label}\" is a reason for sending a case back, not for declining it."
                : $"\"{configured.Label}\" is a reason for declining a case, not for sending it back.");

        return Result.Success(configured.Code);
    }

    /// <summary>The active reasons staff can pick for <paramref name="type"/>, OTHER last.</summary>
    public static IReadOnlyList<RhshfReasonCode> PickList(IEnumerable<RhshfReasonCode> all, RhshfReasonCodeType type)
        => all.Where(c => c.IsActive && (c.Type == type || c.Code == RhshfReasonCode.Other))
            .OrderBy(c => c.Code == RhshfReasonCode.Other)
            .ThenBy(c => c.SortOrder)
            .ThenBy(c => c.Label)
            .ToList();
}
