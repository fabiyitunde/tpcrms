using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

/// <summary>
/// Represents an inbound NAMP application payload received via webhook from Heifer Nigeria / PAYS.
/// Sits in a staging queue until a Loan Officer recalls it into a full NampApplication.
/// </summary>
public class NampStagingRecord : AggregateRoot
{
    // ── Inbound Reference ──────────────────────────────────────────────────
    public string ApplicationReference { get; private set; } = string.Empty;
    public string CrmsApplicationNumber { get; private set; } = string.Empty;
    public string RawPayload { get; private set; } = string.Empty;

    // ── Applicant Info (from payload) ──────────────────────────────────────
    public string ApplicantName { get; private set; } = string.Empty;
    public string BoaAccountNumber { get; private set; } = string.Empty;
    public string? ApplicantPhone { get; private set; }
    public string? ApplicantEmail { get; private set; }
    public NampApplicantCategory ApplicantCategory { get; private set; }

    // ── Equipment Info (from payload) ──────────────────────────────────────
    public string EquipmentDescription { get; private set; } = string.Empty;
    public decimal EquipmentValue { get; private set; }

    // ── Branch Resolution (resolved from BoaAccountNumber) ─────────────────
    public Guid? ResolvedBranchId { get; private set; }
    public Guid? ResolvedOfficeId { get; private set; }
    public Guid? ResolvedLocationId { get; private set; }
    public string? BranchResolutionNote { get; private set; }

    // ── Recall State ───────────────────────────────────────────────────────
    public bool IsRecalled { get; private set; }
    public DateTime? RecalledAt { get; private set; }
    public Guid? RecalledByUserId { get; private set; }

    // ── Timestamps ─────────────────────────────────────────────────────────
    public DateTime ReceivedAt { get; private set; }

    protected NampStagingRecord() { }

    public static NampStagingRecord Create(
        string applicationReference,
        string rawPayload,
        string applicantName,
        string boaAccountNumber,
        NampApplicantCategory applicantCategory,
        string equipmentDescription,
        decimal equipmentValue,
        string? applicantPhone = null,
        string? applicantEmail = null)
    {
        var record = new NampStagingRecord
        {
            ApplicationReference = applicationReference,
            CrmsApplicationNumber = GenerateCrmsApplicationNumber(),
            RawPayload = rawPayload,
            ApplicantName = applicantName,
            BoaAccountNumber = boaAccountNumber,
            ApplicantCategory = applicantCategory,
            EquipmentDescription = equipmentDescription,
            EquipmentValue = equipmentValue,
            ApplicantPhone = applicantPhone,
            ApplicantEmail = applicantEmail,
            ReceivedAt = DateTime.UtcNow,
        };

        // Only genuinely-new staged records raise this (the webhook's re-stage path calls
        // UpdatePayload, not Create) — it drives the "new in your branch queue" notification.
        record.AddDomainEvent(new NampStagingRecordReceivedEvent(record.Id));
        return record;
    }

    public void ResolveBranch(Guid branchId, Guid? officeId, Guid? locationId)
    {
        ResolvedBranchId = branchId;
        ResolvedOfficeId = officeId;
        ResolvedLocationId = locationId;
    }

    public void SetBranchResolutionNote(string note)
    {
        BranchResolutionNote = note;
    }

    private static string GenerateCrmsApplicationNumber()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"NAMP-{DateTime.UtcNow:yyyy}-{suffix}";
    }

    public void UpdatePayload(
        string rawPayload,
        string applicantName,
        string? applicantPhone,
        string? applicantEmail,
        string equipmentDescription,
        decimal equipmentValue,
        NampApplicantCategory applicantCategory)
    {
        RawPayload = rawPayload;
        ApplicantName = applicantName;
        ApplicantPhone = applicantPhone;
        ApplicantEmail = applicantEmail;
        EquipmentDescription = equipmentDescription;
        EquipmentValue = equipmentValue;
        ApplicantCategory = applicantCategory;
    }

    public void MarkRecalled(Guid recalledByUserId)
    {
        IsRecalled = true;
        RecalledAt = DateTime.UtcNow;
        RecalledByUserId = recalledByUserId;
    }
}

// Domain Events
public record NampStagingRecordReceivedEvent(Guid StagingRecordId) : DomainEvent;
