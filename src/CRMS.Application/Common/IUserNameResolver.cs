namespace CRMS.Application.Common;

/// <summary>
/// Turns actor user ids into human-readable display names.
///
/// Before this existed, the codebase had exactly one GUID→name resolution — a local function inside
/// GenerateNampLoanPackCommand (ResolveName, built from a one-shot IUserRepository.GetAllAsync
/// dictionary). Every other surface either denormalised the name at write time (committee members)
/// or simply omitted the actor. RH-SHF did neither and leaked raw GUIDs into the UI.
///
/// Scoped: the underlying lookup is loaded at most once per request/circuit and reused, so a page
/// resolving a dozen actors across several tabs pays for one query, not a dozen.
/// </summary>
public interface IUserNameResolver
{
    /// <summary>Resolves one id. Never returns a raw GUID — unknown ids degrade to a short
    /// truncated form (matching the established GenerateNampLoanPackCommand behaviour) so a stale
    /// or deleted actor never renders as an unreadable 36-character string.</summary>
    Task<string> ResolveAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Nullable convenience — returns null for null input rather than a placeholder, so
    /// callers can distinguish "no actor recorded" from "actor not found".</summary>
    Task<string?> ResolveAsync(Guid? userId, CancellationToken ct = default);

    /// <summary>Resolves many ids against a single load. Ids not found degrade the same way as
    /// the single-id overload.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ResolveManyAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);
}
