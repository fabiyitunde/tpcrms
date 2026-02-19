using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Consent;

/// <summary>
/// Immutable record of consent given by an individual for credit bureau checks.
/// Required for NDPA compliance before any credit data access.
/// </summary>
public class ConsentRecord : AggregateRoot
{
    public Guid? LoanApplicationId { get; private set; }
    public ConsentType ConsentType { get; private set; }
    public ConsentStatus Status { get; private set; }
    
    // Subject Identity
    public string SubjectName { get; private set; } = string.Empty;
    public string? BVN { get; private set; }
    public string? NIN { get; private set; }
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    
    // Consent Details
    public string Purpose { get; private set; } = string.Empty;
    public string ConsentText { get; private set; } = string.Empty;
    public string ConsentVersion { get; private set; } = string.Empty;
    public DateTime ConsentGivenAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    
    // Capture Method
    public ConsentCaptureMethod CaptureMethod { get; private set; }
    public string? SignatureData { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    
    // Witness/Verification
    public Guid? CapturedByUserId { get; private set; }
    public string? CapturedByUserName { get; private set; }
    public string? WitnessName { get; private set; }
    
    // Revocation
    public DateTime? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }

    private ConsentRecord() { }

    public static Result<ConsentRecord> Create(
        string subjectName,
        string? bvn,
        ConsentType consentType,
        string purpose,
        string consentText,
        string consentVersion,
        ConsentCaptureMethod captureMethod,
        Guid? capturedByUserId,
        string? capturedByUserName,
        Guid? loanApplicationId = null,
        string? nin = null,
        string? email = null,
        string? phoneNumber = null,
        string? signatureData = null,
        string? ipAddress = null,
        string? userAgent = null,
        int validityDays = 365)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
            return Result.Failure<ConsentRecord>("Subject name is required");

        if (string.IsNullOrWhiteSpace(purpose))
            return Result.Failure<ConsentRecord>("Consent purpose is required");

        if (string.IsNullOrWhiteSpace(consentText))
            return Result.Failure<ConsentRecord>("Consent text is required");

        if (consentType == ConsentType.CreditBureauCheck && string.IsNullOrWhiteSpace(bvn))
            return Result.Failure<ConsentRecord>("BVN is required for credit bureau consent");

        var consent = new ConsentRecord
        {
            LoanApplicationId = loanApplicationId,
            ConsentType = consentType,
            Status = ConsentStatus.Active,
            SubjectName = subjectName,
            BVN = bvn,
            NIN = nin,
            Email = email,
            PhoneNumber = phoneNumber,
            Purpose = purpose,
            ConsentText = consentText,
            ConsentVersion = consentVersion,
            ConsentGivenAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(validityDays),
            CaptureMethod = captureMethod,
            SignatureData = signatureData,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CapturedByUserId = capturedByUserId,
            CapturedByUserName = capturedByUserName
        };

        consent.AddDomainEvent(new ConsentRecordedEvent(
            consent.Id, subjectName, bvn, consentType, consent.ConsentGivenAt));

        return Result.Success(consent);
    }

    public Result Revoke(string reason)
    {
        if (Status == ConsentStatus.Revoked)
            return Result.Failure("Consent is already revoked");

        if (Status == ConsentStatus.Expired)
            return Result.Failure("Cannot revoke expired consent");

        Status = ConsentStatus.Revoked;
        RevokedAt = DateTime.UtcNow;
        RevocationReason = reason;

        AddDomainEvent(new ConsentRevokedEvent(Id, SubjectName, BVN, reason));

        return Result.Success();
    }

    public bool IsValid()
    {
        return Status == ConsentStatus.Active && DateTime.UtcNow < ExpiresAt;
    }

    public void MarkExpired()
    {
        if (Status == ConsentStatus.Active && DateTime.UtcNow >= ExpiresAt)
        {
            Status = ConsentStatus.Expired;
        }
    }
}

// Domain Events
public record ConsentRecordedEvent(
    Guid ConsentId, string SubjectName, string? BVN, ConsentType ConsentType, DateTime ConsentGivenAt) : DomainEvent;

public record ConsentRevokedEvent(
    Guid ConsentId, string SubjectName, string? BVN, string Reason) : DomainEvent;
