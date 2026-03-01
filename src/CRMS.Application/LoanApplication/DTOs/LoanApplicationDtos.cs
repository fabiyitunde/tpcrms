namespace CRMS.Application.LoanApplication.DTOs;

public record LoanApplicationDto(
    Guid Id,
    string ApplicationNumber,
    string Type,
    string Status,
    Guid LoanProductId,
    string ProductCode,
    string AccountNumber,
    string CustomerId,
    string CustomerName,
    string? RegistrationNumber,
    decimal RequestedAmount,
    string Currency,
    int RequestedTenorMonths,
    decimal InterestRatePerAnnum,
    string InterestRateType,
    string? Purpose,
    decimal? ApprovedAmount,
    int? ApprovedTenorMonths,
    decimal? ApprovedInterestRate,
    Guid InitiatedByUserId,
    Guid? BranchId,
    DateTime? SubmittedAt,
    DateTime? BranchApprovedAt,
    DateTime? FinalApprovedAt,
    DateTime? DisbursedAt,
    string? CoreBankingLoanId,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    List<LoanApplicationDocumentDto> Documents,
    List<LoanApplicationPartyDto> Parties,
    DateTime? IncorporationDate = null
);

public record LoanApplicationSummaryDto(
    Guid Id,
    string ApplicationNumber,
    string Type,
    string Status,
    string ProductCode,
    string CustomerName,
    decimal RequestedAmount,
    string Currency,
    DateTime? SubmittedAt,
    DateTime CreatedAt
);

public record LoanApplicationDocumentDto(
    Guid Id,
    string Category,
    string Status,
    string FileName,
    long FileSize,
    string ContentType,
    string? Description,
    DateTime UploadedAt,
    DateTime? VerifiedAt,
    string? RejectionReason
);

public record LoanApplicationPartyDto(
    Guid Id,
    string PartyType,
    string FullName,
    string? BVN,
    string? Email,
    string? PhoneNumber,
    string? Designation,
    decimal? ShareholdingPercent,
    bool BVNVerified
);

public record LoanApplicationCommentDto(
    Guid Id,
    Guid UserId,
    string Content,
    string? Category,
    DateTime CreatedAt
);

public record LoanApplicationStatusHistoryDto(
    Guid Id,
    string Status,
    Guid ChangedByUserId,
    string? Comment,
    DateTime ChangedAt
);
