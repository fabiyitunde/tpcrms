using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

// ── Add ─────────────────────────────────────────────────────────────────────
public record AddRhshfGuarantorCommand(
    string Reference, Guid ActorUserId, RhshfGuarantorType GuarantorType, string FullName,
    string? Bvn, string? RcNumber, RhshfGuarantorRelationship? RelationshipCategory, string? RelationshipOther,
    string? PhoneNumber, string? Email,
    string? Address, decimal? GuaranteeAmount, string? Notes) : IRequest<ApplicationResult>;

public class AddRhshfGuarantorHandler : IRequestHandler<AddRhshfGuarantorCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfGuarantorRepository _guarantors;
    private readonly IUnitOfWork _uow;

    public AddRhshfGuarantorHandler(IRhshfCreditProfileRepository repo, IRhshfGuarantorRepository guarantors, IUnitOfWork uow)
    {
        _repo = repo;
        _guarantors = guarantors;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AddRhshfGuarantorCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = RhshfGuarantor.Create(
            profile.Id, request.GuarantorType, request.FullName, request.Bvn, request.RcNumber,
            request.RelationshipCategory, request.RelationshipOther,
            request.PhoneNumber, request.Email, request.Address, request.GuaranteeAmount, request.Notes);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        result.Value.SetAuditInfo(request.ActorUserId.ToString(), isNew: true);
        await _guarantors.AddAsync(result.Value, ct);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ── Update ──────────────────────────────────────────────────────────────────
public record UpdateRhshfGuarantorCommand(
    Guid GuarantorId, Guid ActorUserId, RhshfGuarantorType GuarantorType, string FullName,
    string? Bvn, string? RcNumber, RhshfGuarantorRelationship? RelationshipCategory, string? RelationshipOther,
    string? PhoneNumber, string? Email,
    string? Address, decimal? GuaranteeAmount, string? Notes) : IRequest<ApplicationResult>;

public class UpdateRhshfGuarantorHandler : IRequestHandler<UpdateRhshfGuarantorCommand, ApplicationResult>
{
    private readonly IRhshfGuarantorRepository _guarantors;
    private readonly IUnitOfWork _uow;

    public UpdateRhshfGuarantorHandler(IRhshfGuarantorRepository guarantors, IUnitOfWork uow)
    {
        _guarantors = guarantors;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(UpdateRhshfGuarantorCommand request, CancellationToken ct = default)
    {
        var guarantor = await _guarantors.GetByIdAsync(request.GuarantorId, ct);
        if (guarantor is null)
            return ApplicationResult.Failure("Guarantor not found.");

        // The BVN is never sent to the browser (presence only), so a blank BVN on edit means "keep the
        // one on file" rather than wipe it — only a newly typed value replaces it. (A type switch to
        // Corporate still clears it, inside the aggregate.)
        var effectiveBvn = string.IsNullOrWhiteSpace(request.Bvn) ? guarantor.Bvn : request.Bvn;

        var result = guarantor.Update(
            request.GuarantorType, request.FullName, effectiveBvn, request.RcNumber,
            request.RelationshipCategory, request.RelationshipOther,
            request.PhoneNumber, request.Email, request.Address, request.GuaranteeAmount, request.Notes);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        guarantor.SetAuditInfo(request.ActorUserId.ToString());
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ── Update from the FAC's profiling form ────────────────────────────────────

/// <summary>
/// Edits a guarantor from the FAC-facing profiling form, which could previously only add and remove
/// one — so correcting a typo meant deleting the record and re-entering it.
/// <para>
/// Separate from <see cref="UpdateRhshfGuarantorCommand"/> for two reasons. It is scoped to a case
/// reference and refuses a guarantor belonging to anything else: profiling is token-authenticated
/// against one case, so a guarantor id posted from the browser is attacker-controlled and the
/// staff-side command carries no profile scoping at all. And it only writes the fields the FAC's
/// form actually shows — phone, email, address and notes are staff-recorded and have no inputs here,
/// so a full-replace update would silently wipe them.
/// </para>
/// </summary>
public record UpdateRhshfGuarantorFromProfilingCommand(
    string Reference, Guid GuarantorId, RhshfGuarantorType GuarantorType, string FullName,
    string? Bvn, string? RcNumber, RhshfGuarantorRelationship? RelationshipCategory,
    string? RelationshipOther, decimal? GuaranteeAmount) : IRequest<ApplicationResult>;

public class UpdateRhshfGuarantorFromProfilingHandler
    : IRequestHandler<UpdateRhshfGuarantorFromProfilingCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _profiles;
    private readonly IRhshfGuarantorRepository _guarantors;
    private readonly IUnitOfWork _uow;

    public UpdateRhshfGuarantorFromProfilingHandler(
        IRhshfCreditProfileRepository profiles, IRhshfGuarantorRepository guarantors, IUnitOfWork uow)
    {
        _profiles = profiles;
        _guarantors = guarantors;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(
        UpdateRhshfGuarantorFromProfilingCommand request, CancellationToken ct = default)
    {
        var profile = await _profiles.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var guarantor = await _guarantors.GetByIdAsync(request.GuarantorId, ct);
        if (guarantor is null || guarantor.RhshfCreditProfileId != profile.Id)
            return ApplicationResult.Failure("Guarantor not found on this case.");

        // Blank means "keep the one on file": the BVN is never sent to the browser (presence only),
        // so the form has nothing to send back and posting null would wipe it.
        var effectiveBvn = string.IsNullOrWhiteSpace(request.Bvn) ? guarantor.Bvn : request.Bvn;

        // Staff-recorded fields passed through unchanged — the FAC's form has no inputs for them.
        var result = guarantor.Update(
            request.GuarantorType, request.FullName, effectiveBvn, request.RcNumber,
            request.RelationshipCategory, request.RelationshipOther,
            guarantor.PhoneNumber, guarantor.Email, guarantor.Address,
            request.GuaranteeAmount, guarantor.Notes);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ── Remove ──────────────────────────────────────────────────────────────────
public record RemoveRhshfGuarantorCommand(Guid GuarantorId) : IRequest<ApplicationResult>;

public class RemoveRhshfGuarantorHandler : IRequestHandler<RemoveRhshfGuarantorCommand, ApplicationResult>
{
    private readonly IRhshfGuarantorRepository _guarantors;
    private readonly IUnitOfWork _uow;

    public RemoveRhshfGuarantorHandler(IRhshfGuarantorRepository guarantors, IUnitOfWork uow)
    {
        _guarantors = guarantors;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RemoveRhshfGuarantorCommand request, CancellationToken ct = default)
    {
        var guarantor = await _guarantors.GetByIdAsync(request.GuarantorId, ct);
        if (guarantor is null)
            return ApplicationResult.Failure("Guarantor not found.");

        _guarantors.Remove(guarantor);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
