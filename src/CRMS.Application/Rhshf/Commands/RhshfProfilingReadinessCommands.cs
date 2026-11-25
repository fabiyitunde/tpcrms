using CRMS.Application.Access;
using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Access;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Constants;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;

namespace CRMS.Application.Rhshf.Commands;

// BOA's additional profiling requirements (2026-11-25): directors' NIN, ID and phone; management
// staff with their CVs and phones; a named Chief Promoter; and a reviewer's check of every document
// before the case moves towards Credit Risk.

// ── Director identity (FAC) ───────────────────────────────────────────────

/// <summary>
/// The FAC completes a director's NIN, telephone, email and means of identification.
/// <para>
/// A blank NIN keeps the one on file: the profiling form never receives the number back (only
/// whether one is held), so it cannot re-post it, and treating blank as "clear" would wipe it.
/// </para>
/// </summary>
public record UpdateRhshfDirectorIdentityCommand(
    string Reference, Guid DirectorId, string? Nin, string? PhoneNumber, string? Email,
    RhshfMeansOfIdType? MeansOfIdType, string? MeansOfIdNumber, DateTime? MeansOfIdExpiry, Guid UserId)
    : IRequest<ApplicationResult>;

public class UpdateRhshfDirectorIdentityHandler : IRequestHandler<UpdateRhshfDirectorIdentityCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateRhshfDirectorIdentityHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(UpdateRhshfDirectorIdentityCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var director = profile.Directors.FirstOrDefault(d => d.Id == request.DirectorId);
        if (director is null)
            return ApplicationResult.Failure("Director not found on this case.");

        var result = RhshfProfilingPeople.ApplyIdentity(director, request.Nin, request.PhoneNumber, request.Email,
            request.MeansOfIdType, request.MeansOfIdNumber, request.MeansOfIdExpiry);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        profile.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

public static class RhshfProfilingPeople
{
    /// <summary>Applies the FAC's identity entries to a director; a blank NIN or ID number keeps the one held.</summary>
    public static Domain.Common.Result ApplyIdentity(RhshfDirector director, string? nin, string? phoneNumber, string? email,
        RhshfMeansOfIdType? idType, string? idNumber, DateTime? idExpiry)
    {
        if (!string.IsNullOrWhiteSpace(nin))
        {
            var ninResult = director.UpdateNin(nin);
            if (ninResult.IsFailure)
                return ninResult;
        }

        // Like the NIN, the ID number is shown back masked, so a blank one keeps what is held.
        if (idType is not null && string.IsNullOrWhiteSpace(idNumber))
            idNumber = director.MeansOfIdNumber;

        var idResult = director.UpdateMeansOfId(idType, idNumber, idExpiry);
        if (idResult.IsFailure)
            return idResult;

        director.UpdateContact(phoneNumber, email);
        return Domain.Common.Result.Success();
    }
}

// ── Management staff & Chief Promoter (FAC) ───────────────────────────────

public record AddRhshfManagementStaffCommand(
    string Reference, string FullName, string Position, string? PhoneNumber, string? Email, Guid UserId)
    : IRequest<ApplicationResult>;

public class AddRhshfManagementStaffHandler : IRequestHandler<AddRhshfManagementStaffCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public AddRhshfManagementStaffHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AddRhshfManagementStaffCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.AddManagementStaff(request.FullName, request.Position, request.PhoneNumber, request.Email);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        profile.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

public record UpdateRhshfManagementStaffCommand(
    string Reference, Guid StaffId, string FullName, string Position, string? PhoneNumber, string? Email, Guid UserId)
    : IRequest<ApplicationResult>;

public class UpdateRhshfManagementStaffHandler : IRequestHandler<UpdateRhshfManagementStaffCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateRhshfManagementStaffHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(UpdateRhshfManagementStaffCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.UpdateManagementStaff(request.StaffId, request.FullName, request.Position, request.PhoneNumber, request.Email);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        profile.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

public record RemoveRhshfManagementStaffCommand(string Reference, Guid StaffId, Guid UserId) : IRequest<ApplicationResult>;

public class RemoveRhshfManagementStaffHandler : IRequestHandler<RemoveRhshfManagementStaffCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _uow;

    public RemoveRhshfManagementStaffHandler(IRhshfCreditProfileRepository repo, IFileStorageService fileStorage, IUnitOfWork uow)
    {
        _repo = repo;
        _fileStorage = fileStorage;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RemoveRhshfManagementStaffCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        // Captured before the aggregate drops the person's documents, so their files can follow.
        var orphanedPaths = profile.SupportingDocuments
            .Where(d => d.ManagementStaffId == request.StaffId)
            .Select(d => d.StoragePath)
            .ToList();

        var result = profile.RemoveManagementStaff(request.StaffId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        profile.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        foreach (var path in orphanedPaths)
        {
            try { await _fileStorage.DeleteAsync(path, ct); }
            catch { /* the row is gone; a stray blob is harmless and not worth failing the removal over */ }
        }
        return ApplicationResult.Success();
    }
}

/// <summary>Names the Chief Promoter (a director or manager), or clears it with a null id.</summary>
public record SetRhshfChiefPromoterCommand(string Reference, Guid? PersonId, Guid UserId) : IRequest<ApplicationResult>;

public class SetRhshfChiefPromoterHandler : IRequestHandler<SetRhshfChiefPromoterCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public SetRhshfChiefPromoterHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(SetRhshfChiefPromoterCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.SetChiefPromoter(request.PersonId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        profile.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ── Document verification (staff) ─────────────────────────────────────────

/// <summary>A Credit Officer marks one FAC document Verified, or Rejected with a reason the FAC will
/// see. Role checked here, not only in the page: only CreditOfficer and SystemAdmin may.</summary>
public record ReviewRhshfSupportingDocumentCommand(
    string Reference, Guid DocumentId, bool Verified, string? Reason, Guid UserId)
    : IRequest<ApplicationResult>;

public class ReviewRhshfSupportingDocumentHandler : IRequestHandler<ReviewRhshfSupportingDocumentCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _uow;
    private readonly AuditService? _audit;
    private readonly IUserNameResolver? _names;

    public ReviewRhshfSupportingDocumentHandler(IRhshfCreditProfileRepository repo, IRoleRepository roles, IUnitOfWork uow,
        AuditService? audit = null, IUserNameResolver? names = null)
    {
        _repo = repo;
        _roles = roles;
        _uow = uow;
        _audit = audit;
        _names = names;
    }

    public async Task<ApplicationResult> Handle(ReviewRhshfSupportingDocumentCommand request, CancellationToken ct = default)
    {
        var roles = (await _roles.GetUserRolesAsync(request.UserId, ct)).Select(r => r.Name).ToList();
        if (!roles.Contains(Roles.CreditOfficer) && !roles.Contains(Roles.SystemAdmin))
            return ApplicationResult.Failure("Only a Credit Officer can verify or reject documents.");

        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = request.Verified
            ? profile.VerifySupportingDocument(request.DocumentId, request.UserId)
            : profile.RejectSupportingDocument(request.DocumentId, request.UserId, request.Reason ?? string.Empty);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        profile.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        if (_audit is not null)
        {
            try
            {
                var document = profile.SupportingDocuments.First(d => d.Id == request.DocumentId);
                var userName = _names is null ? request.UserId.ToString() : await _names.ResolveAsync(request.UserId, ct);
                await _audit.LogAsync(
                    AuditAction.DocumentVerify, AuditCategory.Document,
                    $"{(request.Verified ? "Verified" : "Rejected")} RH-SHF document {document.FileName} ({document.Category})",
                    nameof(RhshfSupportingDocument), document.Id, profile.Reference,
                    request.UserId, userName, string.Join(",", roles),
                    newValues: new { Status = document.VerificationStatus.ToString(), document.RejectionReason }, ct: ct);
            }
            catch
            {
                // The decision is saved; a failed audit line must not report it as failed.
            }
        }
        return ApplicationResult.Success();
    }
}

// ── Credit Risk Readiness (staff) ─────────────────────────────────────────

public record RhshfReadinessItemDto(
    string Title, string? PersonName, bool IsMandatory, string Section, string Status, string? Note,
    bool IsDocument, Guid? DocumentId);

public record RhshfReadinessDto(
    IReadOnlyList<RhshfReadinessItemDto> Items,
    /// <summary>Why Proceed at appraisal is refused right now; empty when it is not.</summary>
    IReadOnlyList<string> AppraisalBlockers);

public record GetRhshfReadinessQuery(string Reference) : IRequest<ApplicationResult<RhshfReadinessDto>>;

public class GetRhshfReadinessHandler : IRequestHandler<GetRhshfReadinessQuery, ApplicationResult<RhshfReadinessDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfDocumentRequirementRepository _documentRequirements;
    private readonly IRhshfProfilingFieldRequirementRepository _fieldRequirements;

    public GetRhshfReadinessHandler(IRhshfCreditProfileRepository repo,
        IRhshfDocumentRequirementRepository documentRequirements, IRhshfProfilingFieldRequirementRepository fieldRequirements)
    {
        _repo = repo;
        _documentRequirements = documentRequirements;
        _fieldRequirements = fieldRequirements;
    }

    public async Task<ApplicationResult<RhshfReadinessDto>> Handle(GetRhshfReadinessQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfReadinessDto>.Failure("Case not found.");

        var documents = await _documentRequirements.GetActiveAsync(ct);
        var fields = await _fieldRequirements.GetAllAsync(ct);

        var items = RhshfProfilingReadiness.Evaluate(profile, documents, fields, DateTime.UtcNow)
            .Select(i => new RhshfReadinessItemDto(i.Title, i.PersonName, i.IsMandatory, i.Section.ToString(),
                i.Status.ToString(), i.Note, i.IsDocument, i.DocumentId))
            .ToList();
        return ApplicationResult<RhshfReadinessDto>.Success(
            new RhshfReadinessDto(items, RhshfProfilingReadiness.AppraisalBlockers(profile, documents)));
    }
}

// ── Admin: field requirements ─────────────────────────────────────────────

public record RhshfProfilingFieldRequirementDto(Guid Id, string Key, string Title, string? Description, bool IsMandatory);

public record GetRhshfProfilingFieldRequirementsQuery : IRequest<ApplicationResult<List<RhshfProfilingFieldRequirementDto>>>;

public class GetRhshfProfilingFieldRequirementsHandler
    : IRequestHandler<GetRhshfProfilingFieldRequirementsQuery, ApplicationResult<List<RhshfProfilingFieldRequirementDto>>>
{
    private readonly IRhshfProfilingFieldRequirementRepository _repo;

    public GetRhshfProfilingFieldRequirementsHandler(IRhshfProfilingFieldRequirementRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<RhshfProfilingFieldRequirementDto>>> Handle(
        GetRhshfProfilingFieldRequirementsQuery request, CancellationToken ct = default)
    {
        var rows = await _repo.GetAllAsync(ct);
        return ApplicationResult<List<RhshfProfilingFieldRequirementDto>>.Success(rows
            .Select(r => new RhshfProfilingFieldRequirementDto(r.Id, r.Key.ToString(), r.Title, r.Description, r.IsMandatory))
            .ToList());
    }
}

public record SetRhshfProfilingFieldRequirementCommand(Guid Id, bool IsMandatory, Guid UserId, string UserName)
    : IRequest<ApplicationResult>;

public class SetRhshfProfilingFieldRequirementHandler : IRequestHandler<SetRhshfProfilingFieldRequirementCommand, ApplicationResult>
{
    private readonly IRhshfProfilingFieldRequirementRepository _repo;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _uow;
    private readonly AuditService? _audit;

    public SetRhshfProfilingFieldRequirementHandler(IRhshfProfilingFieldRequirementRepository repo, IRoleRepository roles,
        IUnitOfWork uow, AuditService? audit = null)
    {
        _repo = repo;
        _roles = roles;
        _uow = uow;
        _audit = audit;
    }

    public async Task<ApplicationResult> Handle(SetRhshfProfilingFieldRequirementCommand request, CancellationToken ct = default)
    {
        var roles = (await _roles.GetUserRolesAsync(request.UserId, ct)).Select(r => r.Name);
        if (!roles.Contains(Roles.SystemAdmin))
            return ApplicationResult.Failure("Only a system administrator can change profiling requirements.");

        var row = await _repo.GetByIdAsync(request.Id, ct);
        if (row is null)
            return ApplicationResult.Failure("Requirement not found.");
        if (row.IsMandatory == request.IsMandatory)
            return ApplicationResult.Success();

        row.SetMandatory(request.IsMandatory);
        row.SetAuditInfo(request.UserName);
        await _uow.SaveChangesAsync(ct);

        if (_audit is not null)
            await _audit.LogAsync(AuditAction.ConfigChange, AuditCategory.Configuration,
                $"RH-SHF profiling requirement \"{row.Title}\" set to {(request.IsMandatory ? "mandatory" : "optional")}.",
                nameof(RhshfProfilingFieldRequirement), row.Id, row.Key.ToString(), request.UserId, request.UserName,
                Roles.SystemAdmin, newValues: new { row.IsMandatory }, ct: ct);
        return ApplicationResult.Success();
    }
}

// ── Reveal a director's BVN or NIN (staff) ────────────────────────────────

/// <summary>Returns one director's full BVN or NIN to a user allowed
/// <see cref="FeatureKeys.RhshfIdentityReveal"/>, and writes the access to the audit trail. Every
/// other path shows these masked.</summary>
public record RevealRhshfDirectorIdentityQuery(string Reference, Guid DirectorId, bool Nin, Guid UserId)
    : IRequest<ApplicationResult<string>>;

public class RevealRhshfDirectorIdentityHandler : IRequestHandler<RevealRhshfDirectorIdentityQuery, ApplicationResult<string>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IFeatureAccessService _access;
    private readonly AuditService _audit;
    private readonly IUserNameResolver _names;

    public RevealRhshfDirectorIdentityHandler(IRhshfCreditProfileRepository repo, IFeatureAccessService access,
        AuditService audit, IUserNameResolver names)
    {
        _repo = repo;
        _access = access;
        _audit = audit;
        _names = names;
    }

    public async Task<ApplicationResult<string>> Handle(RevealRhshfDirectorIdentityQuery request, CancellationToken ct = default)
    {
        if (!await _access.IsAllowedAsync(request.UserId, FeatureKeys.RhshfIdentityReveal, ct))
            return ApplicationResult<string>.Failure("You do not have access to full BVNs and NINs.");

        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        var director = profile?.Directors.FirstOrDefault(d => d.Id == request.DirectorId);
        if (director is null)
            return ApplicationResult<string>.Failure("Director not found on this case.");

        var value = request.Nin ? director.Nin : director.Bvn;
        if (string.IsNullOrWhiteSpace(value))
            return ApplicationResult<string>.Failure(request.Nin ? "No NIN is on file." : "No BVN is on file.");

        // Logged before the value is returned: a reveal that cannot be recorded is not given.
        await _audit.LogDataAccessAsync(request.UserId, await _names.ResolveAsync(request.UserId, ct), string.Empty,
            request.Nin ? SensitiveDataType.NIN : SensitiveDataType.BVN, nameof(RhshfDirector), director.Id,
            DataAccessType.View, entityReference: profile!.Reference,
            accessReason: $"Revealed {(request.Nin ? "NIN" : "BVN")} of director {director.FullName}", ct: ct);
        return ApplicationResult<string>.Success(value);
    }
}
