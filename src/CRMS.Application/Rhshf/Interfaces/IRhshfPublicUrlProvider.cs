namespace CRMS.Application.Rhshf.Interfaces;

/// <summary>
/// Resolves the public, FAC-facing URLs for a case.
///
/// The routes and the host live in Infrastructure configuration, but the Application layer needs
/// them to populate the portal-facing contract (the status response's actionUrl, and the OfferReady
/// webhook's). This is the same shape as IUserNameResolver: an Application-owned abstraction over
/// something only Infrastructure can answer.
///
/// Every method returns null when no public host is configured. That is deliberate — the previous
/// behaviour built URLs on the placeholder "crms.example.com" and handed them to the portal as
/// though they were real, which is what left FACs clicking through to nowhere. A missing URL the
/// portal can detect is strictly better than a plausible one that does not resolve.
/// </summary>
public interface IRhshfPublicUrlProvider
{
    /// <summary>The FAC-facing profiling form for a case, without a token appended.</summary>
    string? ProfilingUrl(string reference);

    /// <summary>The FAC-facing offer acceptance page for a case, without a token appended.</summary>
    string? OfferUrl(string reference);
}
