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

/// <summary>
/// One EOP line on the wire from the portal.
/// <para>
/// Supplier, Product and UnitOfMeasurement are optional so the portal can begin sending them
/// independently of this deployment: a payload without them is accepted exactly as before. The
/// offer letter's implementation plan names the supplier per line where one is sent, and states the
/// quantity in the unit given rather than assuming kilogrammes — a line can be counted in bags or
/// visits.
/// </para>
/// </summary>
public record RhshfEopLineDto(
    string Commodity,
    decimal QuantityKg,
    decimal UnitPricePerKg,
    decimal LineValue,
    string? Supplier = null,
    string? Product = null,
    string? UnitOfMeasurement = null)
{
    /// <summary>Quantity with its unit — mirrors RhshfEopLine.QuantityDisplay, so the staff-side
    /// tables and the generated documents word it identically. Falls back to kilogrammes for lines
    /// recorded before the portal sent a unit.</summary>
    public string QuantityDisplay =>
        $"{QuantityKg:N0} {(string.IsNullOrWhiteSpace(UnitOfMeasurement) ? "kg" : UnitOfMeasurement.Trim())}";

    /// <summary>The product where one was sent, otherwise the commodity.</summary>
    public string ItemDescription =>
        string.IsNullOrWhiteSpace(Product) ? Commodity : Product.Trim();
}

public record RhshfProgrammeDto(string Code, string Name);

public record RhshfSessionDto(string Code, string Name);

public record RhshfFacContactDto(string? Email, string? Phone);

public record RhshfFacDto(
    Guid FacId,
    string CompanyName,
    string RcNumber,
    string? Tin,
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
