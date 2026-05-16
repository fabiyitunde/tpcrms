using CRMS.Application.OfferAcceptance.DTOs;

namespace CRMS.Application.OfferAcceptance.Interfaces;

/// <summary>
/// Generates the Disbursement Memo PDF at OfferAccepted stage.
/// Summarises all CP items (satisfied/waived), CS items (with due dates),
/// and serves as the official pre-disbursement clearance document for audit/CBN purposes.
/// </summary>
public interface IDisbursementMemoPdfGenerator
{
    Task<byte[]> GenerateAsync(
        DisbursementMemoRequest request,
        CancellationToken ct = default);
}

public record DisbursementMemoRequest(
    string ApplicationNumber,
    string CustomerName,
    decimal ApprovedAmount,
    int ApprovedTenorMonths,
    decimal ApprovedInterestRate,
    DateTime OfferIssuedAt,
    DateTime OfferAcceptedAt,
    string AcceptedByUserName,
    string BankName,
    List<DisbursementChecklistItemDto> ChecklistItems
);

public record DisbursementChecklistItemDto(
    string ItemName,
    string ConditionType,
    bool IsMandatory,
    string Status,
    string? SatisfiedByUserName,
    DateTime? SatisfiedAt,
    string? WaiverProposedByUserName,
    string? WaiverReason,
    string? WaiverApprovedByUserName,
    DateTime? WaiverRatifiedAt,
    DateTime? DueDate
);
