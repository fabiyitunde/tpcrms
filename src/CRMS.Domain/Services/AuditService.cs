using System.Text.Json;
using CRMS.Domain.Aggregates.Audit;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Domain.Services;

/// <summary>
/// Domain service for creating audit logs.
/// Provides convenient methods for logging various types of actions.
/// </summary>
public class AuditService
{
    private readonly IAuditLogRepository _auditRepository;
    private readonly IDataAccessLogRepository _dataAccessRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AuditService(
        IAuditLogRepository auditRepository,
        IDataAccessLogRepository dataAccessRepository,
        IUnitOfWork unitOfWork)
    {
        _auditRepository = auditRepository;
        _dataAccessRepository = dataAccessRepository;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Log a general audit event.
    /// </summary>
    public async Task LogAsync(
        AuditAction action,
        AuditCategory category,
        string description,
        string entityType,
        Guid? entityId = null,
        string? entityReference = null,
        Guid? userId = null,
        string? userName = null,
        string? userRole = null,
        string? ipAddress = null,
        Guid? loanApplicationId = null,
        string? loanApplicationNumber = null,
        object? oldValues = null,
        object? newValues = null,
        object? additionalData = null,
        bool isSuccess = true,
        string? errorMessage = null,
        CancellationToken ct = default)
    {
        var log = AuditLog.Create(
            action,
            category,
            description,
            entityType,
            entityId,
            entityReference,
            userId,
            userName,
            userRole,
            ipAddress,
            null,
            loanApplicationId,
            loanApplicationNumber,
            oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            newValues != null ? JsonSerializer.Serialize(newValues) : null,
            additionalData != null ? JsonSerializer.Serialize(additionalData) : null,
            isSuccess,
            errorMessage);

        await _auditRepository.AddAsync(log, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Log loan application status change.
    /// </summary>
    public async Task LogStatusChangeAsync(
        Guid loanApplicationId,
        string applicationNumber,
        string oldStatus,
        string newStatus,
        Guid userId,
        string userName,
        string userRole,
        string? comment = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        await LogAsync(
            AuditAction.StatusChange,
            AuditCategory.LoanApplication,
            $"Loan application status changed from {oldStatus} to {newStatus}",
            "LoanApplication",
            loanApplicationId,
            applicationNumber,
            userId,
            userName,
            userRole,
            ipAddress,
            loanApplicationId,
            applicationNumber,
            new { Status = oldStatus },
            new { Status = newStatus, Comment = comment },
            ct: ct);
    }

    /// <summary>
    /// Log credit bureau request.
    /// </summary>
    public async Task LogCreditBureauRequestAsync(
        Guid bureauReportId,
        string bureau,
        string subjectType,
        string subjectName,
        Guid? loanApplicationId,
        string? applicationNumber,
        Guid userId,
        string userName,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        await LogAsync(
            AuditAction.BureauRequest,
            AuditCategory.CreditBureau,
            $"Credit bureau request sent to {bureau} for {subjectType}: {subjectName}",
            "BureauReport",
            bureauReportId,
            null,
            userId,
            userName,
            null,
            ipAddress,
            loanApplicationId,
            applicationNumber,
            additionalData: new { Bureau = bureau, SubjectType = subjectType, SubjectName = subjectName },
            ct: ct);
    }

    /// <summary>
    /// Log committee vote.
    /// </summary>
    public async Task LogCommitteeVoteAsync(
        Guid committeeReviewId,
        Guid loanApplicationId,
        string applicationNumber,
        string vote,
        Guid userId,
        string userName,
        string userRole,
        string? comment = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        await LogAsync(
            AuditAction.Vote,
            AuditCategory.Committee,
            $"Committee vote cast: {vote}",
            "CommitteeReview",
            committeeReviewId,
            null,
            userId,
            userName,
            userRole,
            ipAddress,
            loanApplicationId,
            applicationNumber,
            newValues: new { Vote = vote, Comment = comment },
            ct: ct);
    }

    /// <summary>
    /// Log committee decision.
    /// </summary>
    public async Task LogCommitteeDecisionAsync(
        Guid committeeReviewId,
        Guid loanApplicationId,
        string applicationNumber,
        string decision,
        decimal? approvedAmount,
        int? approvedTenor,
        decimal? approvedRate,
        string rationale,
        Guid userId,
        string userName,
        string userRole,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        await LogAsync(
            AuditAction.Decision,
            AuditCategory.Committee,
            $"Committee decision recorded: {decision}",
            "CommitteeReview",
            committeeReviewId,
            null,
            userId,
            userName,
            userRole,
            ipAddress,
            loanApplicationId,
            applicationNumber,
            newValues: new { 
                Decision = decision, 
                ApprovedAmount = approvedAmount, 
                ApprovedTenor = approvedTenor,
                ApprovedRate = approvedRate,
                Rationale = rationale 
            },
            ct: ct);
    }

    /// <summary>
    /// Log configuration change request.
    /// </summary>
    public async Task LogConfigChangeRequestAsync(
        Guid parameterId,
        string category,
        string parameterKey,
        decimal oldValue,
        decimal newValue,
        string reason,
        Guid userId,
        string userName,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        await LogAsync(
            AuditAction.ConfigChange,
            AuditCategory.Configuration,
            $"Configuration change requested: {category}.{parameterKey}",
            "ScoringParameter",
            parameterId,
            $"{category}.{parameterKey}",
            userId,
            userName,
            null,
            ipAddress,
            oldValues: new { Value = oldValue },
            newValues: new { Value = newValue, Reason = reason },
            ct: ct);
    }

    /// <summary>
    /// Log configuration change approval.
    /// </summary>
    public async Task LogConfigChangeApprovalAsync(
        Guid parameterId,
        string category,
        string parameterKey,
        decimal oldValue,
        decimal newValue,
        Guid approvedByUserId,
        string approvedByUserName,
        string? notes = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        await LogAsync(
            AuditAction.ConfigApprove,
            AuditCategory.Configuration,
            $"Configuration change approved: {category}.{parameterKey}",
            "ScoringParameter",
            parameterId,
            $"{category}.{parameterKey}",
            approvedByUserId,
            approvedByUserName,
            null,
            ipAddress,
            oldValues: new { Value = oldValue },
            newValues: new { Value = newValue, ApprovalNotes = notes },
            ct: ct);
    }

    /// <summary>
    /// Log login attempt.
    /// </summary>
    public async Task LogLoginAsync(
        Guid? userId,
        string? userName,
        string? email,
        bool isSuccess,
        string? failureReason = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        var log = AuditLog.Create(
            isSuccess ? AuditAction.Login : AuditAction.LoginFailed,
            AuditCategory.Authentication,
            isSuccess ? $"User logged in: {userName ?? email}" : $"Login failed for: {userName ?? email}",
            "User",
            userId,
            userName ?? email,
            userId,
            userName,
            null,
            ipAddress,
            userAgent,
            isSuccess: isSuccess,
            errorMessage: failureReason);

        await _auditRepository.AddAsync(log, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Log sensitive data access.
    /// </summary>
    public async Task LogDataAccessAsync(
        Guid userId,
        string userName,
        string userRole,
        SensitiveDataType dataType,
        string entityType,
        Guid entityId,
        DataAccessType accessType,
        string? entityReference = null,
        Guid? loanApplicationId = null,
        string? loanApplicationNumber = null,
        string? accessReason = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var log = DataAccessLog.Create(
            userId,
            userName,
            userRole,
            dataType,
            entityType,
            entityId,
            accessType,
            entityReference,
            loanApplicationId,
            loanApplicationNumber,
            accessReason,
            ipAddress);

        await _dataAccessRepository.AddAsync(log, ct);

        // Also log to main audit log
        await LogAsync(
            AuditAction.Read,
            AuditCategory.DataAccess,
            $"Sensitive data accessed: {dataType} ({accessType})",
            entityType,
            entityId,
            entityReference,
            userId,
            userName,
            userRole,
            ipAddress,
            loanApplicationId,
            loanApplicationNumber,
            additionalData: new { DataType = dataType.ToString(), AccessType = accessType.ToString(), Reason = accessReason },
            ct: ct);
    }

    /// <summary>
    /// Log document action.
    /// </summary>
    public async Task LogDocumentActionAsync(
        AuditAction action,
        Guid documentId,
        string fileName,
        string documentType,
        Guid? loanApplicationId,
        string? applicationNumber,
        Guid userId,
        string userName,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        await LogAsync(
            action,
            AuditCategory.Document,
            $"Document {action.ToString().ToLower()}: {fileName}",
            documentType,
            documentId,
            fileName,
            userId,
            userName,
            null,
            ipAddress,
            loanApplicationId,
            applicationNumber,
            additionalData: new { FileName = fileName, DocumentType = documentType },
            ct: ct);
    }
}
