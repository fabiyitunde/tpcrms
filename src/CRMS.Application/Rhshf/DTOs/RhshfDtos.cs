namespace CRMS.Application.Rhshf.DTOs;

/// <summary>Response shape for POST /v1/credit-profiles, matching brief §4.1 exactly.</summary>
public record SubmitConsolidatedEopResultDto(
    string Reference,
    string Token,
    string ProfilingUrl,
    DateTime TokenExpiresAt,
    string Status);

/// <summary>Response shape for POST /v1/credit-profiles/{reference}/token, matching brief §4.6.</summary>
public record RhshfTokenRefreshResultDto(string Token, DateTime TokenExpiresAt);

/// <summary>Internal result of verifying+consuming a profiling token — not part of the brief's wire
/// contract; consumed by Phase 3's Razor Pages front door, not the portal.</summary>
public record RhshfTokenVerificationResultDto(string Reference, string Status, string? CurrentStage);

public record RhshfEopLineDto(string Commodity, decimal QuantityKg, decimal UnitPricePerKg, decimal LineValue);

public record RhshfProgrammeDto(string Code, string Name);

public record RhshfSessionDto(string Code, string Name);

public record RhshfFacContactDto(string? Email, string? Phone);

public record RhshfFacDto(
    Guid FacId,
    string CompanyName,
    string RcNumber,
    string Tin,
    string BoaAccountNumber,
    RhshfFacContactDto? Contact,
    string? State,
    string? Lga);

/// <summary>Request shape for POST /v1/credit-profiles, matching brief §4.1 exactly.</summary>
public record SubmitConsolidatedEopRequest(
    Guid SubmissionId,
    RhshfProgrammeDto Programme,
    RhshfSessionDto Session,
    RhshfFacDto Fac,
    decimal TotalEopValue,
    string Currency,
    int? FarmerCount,
    List<RhshfEopLineDto>? EopLines,
    string CallbackUrl,
    Dictionary<string, string>? Metadata);
