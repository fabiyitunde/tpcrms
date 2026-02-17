namespace CRMS.Domain.Enums;

public enum FinancialYearType
{
    Audited,
    Reviewed,
    ManagementAccounts,
    Interim,
    Projected
}

public enum FinancialStatementStatus
{
    Draft,
    PendingReview,
    Verified,
    Rejected
}

public enum InputMethod
{
    ManualEntry,
    ExcelUpload,
    PdfExtraction,
    ApiImport
}
