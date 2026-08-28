namespace CRMS.Application.Rhshf.Interfaces;

/// <summary>
/// Issues the CRMS-signed reference token (§4.2 of the integration brief) that authenticates a FAC
/// into the profiling form. Own signing key/claims — independent of staff-login's TokenService and
/// of any NAMP token code. Verification, single-use enforcement, and refresh are Phase 2 (not yet
/// implemented) — this phase only needs issuance so the submit endpoint can return a real token.
/// </summary>
public interface IRhshfTokenService
{
    RhshfIssuedTokenResult IssueToken(Guid rhshfCreditProfileId, string reference, Guid facId, string programmeCode);

    /// <summary>Validates signature, issuer/audience, and expiry only — does not know about
    /// single-use/consumption, which is a case-level business rule (RhshfCreditProfile.ConsumeToken).
    /// Returns null on any validation failure (expired, tampered, wrong signer, etc).</summary>
    RhshfTokenValidationResult? ValidateToken(string token);
}

/// <summary>ProfilingUrl is fully-formed here (base URL + token) so the base URL config stays in
/// Infrastructure — the Application layer only references CRMS.Domain, never CRMS.Infrastructure.</summary>
public record RhshfIssuedTokenResult(string Token, string Jti, DateTime ExpiresAt, string ProfilingUrl);

public record RhshfTokenValidationResult(string Reference, Guid FacId, string ProgrammeCode, string Jti);
