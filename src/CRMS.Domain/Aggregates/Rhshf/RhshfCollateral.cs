using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// A collateral instrument (RSHSF_Programme_Details.docx S/N 16) — recorded by the Legal Officer as
/// part of Legal Clearance. Own aggregate root, own table, mirroring NampCollateral's "one flexible
/// entity, nullable fields per type" shape rather than the generic Collateral aggregate (hard-tied
/// to LoanApplicationId, not reusable). A case may carry more than one instrument (e.g. a Bank
/// Guarantee and a mortgage together), so this is append-only per cycle, not a singleton.
/// </summary>
public class RhshfCollateral : AggregateRoot
{
    public Guid RhshfCreditProfileId { get; private set; }
    public int CycleNumber { get; private set; }
    public RhshfCollateralType Type { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public DateTime? IssuedDate { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public Guid RecordedBy { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public string? Notes { get; private set; }

    // ── Bank Guarantee ────────────────────────────────────────────────────
    public string? GuarantorBankName { get; private set; }
    public decimal? GuaranteeAmount { get; private set; }
    public bool? IsUnconditional { get; private set; }

    // ── NIRSAL CRG ────────────────────────────────────────────────────────
    public decimal? CrgCoveragePercentage { get; private set; }

    // ── Legal Mortgage ────────────────────────────────────────────────────
    public string? PropertyDescription { get; private set; }
    public decimal? PropertyValue { get; private set; }
    public string? TitleReferenceNumber { get; private set; }
    public string? RegistrationAuthority { get; private set; }
    public RhshfCollateralPerfectionStatus? PerfectionStatus { get; private set; }

    private readonly List<RhshfCollateralDocument> _documents = [];
    public IReadOnlyCollection<RhshfCollateralDocument> Documents => _documents.AsReadOnly();

    protected RhshfCollateral() { }

    public static Result<RhshfCollateral> Create(
        Guid rhshfCreditProfileId, int cycleNumber, RhshfCollateralType type, Guid recordedBy, string? notes,
        string? referenceNumber, DateTime? issuedDate, DateTime? expiryDate,
        string? guarantorBankName, decimal? guaranteeAmount, bool? isUnconditional,
        decimal? crgCoveragePercentage,
        string? propertyDescription, decimal? propertyValue, string? titleReferenceNumber,
        string? registrationAuthority, RhshfCollateralPerfectionStatus? perfectionStatus)
    {
        if (recordedBy == Guid.Empty)
            return Result.Failure<RhshfCollateral>("recordedBy is required.");

        switch (type)
        {
            case RhshfCollateralType.BankGuarantee when string.IsNullOrWhiteSpace(guarantorBankName) || guaranteeAmount is null or <= 0:
                return Result.Failure<RhshfCollateral>("A Bank Guarantee requires a guarantor bank name and a positive guarantee amount.");
            case RhshfCollateralType.NirsalCrg when crgCoveragePercentage is null or <= 0:
                return Result.Failure<RhshfCollateral>("A NIRSAL CRG requires a positive coverage percentage.");
            case RhshfCollateralType.LegalMortgage when string.IsNullOrWhiteSpace(propertyDescription) || propertyValue is null or <= 0:
                return Result.Failure<RhshfCollateral>("A Legal Mortgage requires a property description and a positive property value.");
        }

        return Result.Success(new RhshfCollateral
        {
            RhshfCreditProfileId = rhshfCreditProfileId,
            CycleNumber = cycleNumber,
            Type = type,
            ReferenceNumber = referenceNumber,
            IssuedDate = issuedDate,
            ExpiryDate = expiryDate,
            RecordedBy = recordedBy,
            RecordedAt = DateTime.UtcNow,
            Notes = notes,
            GuarantorBankName = guarantorBankName,
            GuaranteeAmount = guaranteeAmount,
            IsUnconditional = isUnconditional,
            CrgCoveragePercentage = crgCoveragePercentage,
            PropertyDescription = propertyDescription,
            PropertyValue = propertyValue,
            TitleReferenceNumber = titleReferenceNumber,
            RegistrationAuthority = registrationAuthority,
            PerfectionStatus = perfectionStatus,
        });
    }

    public RhshfCollateralDocument AddDocument(string fileName, string contentType, string storagePath, long sizeBytes)
    {
        var document = new RhshfCollateralDocument(Id, fileName, contentType, storagePath, sizeBytes);
        _documents.Add(document);
        return document;
    }
}
