using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

// ── Admin: manage the FAC required-document checklist ──────────────────────
// Same shape as the pre-deployment checklist templates — the rules the FAC form enforces are
// bank policy, not code, so they live in a table an administrator can change.

public record CreateRhshfDocumentRequirementCommand(
    RhshfDocumentCategory Category, string Title, string? Description, bool IsMandatory, int SortOrder,
    /// <summary>Formats the slot accepts. Empty accepts anything, which is how every slot behaved
    /// before format rules existed.</summary>
    IReadOnlyList<RhshfDocumentFormat>? AllowedFormats = null,
    bool MustMatchFinancialTemplate = false)
    : IRequest<ApplicationResult<RhshfDocumentRequirementDto>>;

public class CreateRhshfDocumentRequirementHandler
    : IRequestHandler<CreateRhshfDocumentRequirementCommand, ApplicationResult<RhshfDocumentRequirementDto>>
{
    private readonly IRhshfDocumentRequirementRepository _repo;
    private readonly IUnitOfWork _uow;

    public CreateRhshfDocumentRequirementHandler(IRhshfDocumentRequirementRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfDocumentRequirementDto>> Handle(
        CreateRhshfDocumentRequirementCommand request, CancellationToken ct = default)
    {
        // One rule per category: two rows for the same category would render as duplicate upload
        // slots on the FAC form and make "is this satisfied?" ambiguous.
        var existing = await _repo.GetAllAsync(ct);
        if (existing.Any(r => r.Category == request.Category))
            return ApplicationResult<RhshfDocumentRequirementDto>.Failure(
                $"A requirement for '{request.Category}' already exists — edit it instead.");

        // A format rule only binds a workbook, so asking for the financial-statements template
        // without accepting spreadsheets would store a rule that can never fire.
        if (request.MustMatchFinancialTemplate
            && !(request.AllowedFormats ?? []).Contains(RhshfDocumentFormat.Spreadsheet))
            return ApplicationResult<RhshfDocumentRequirementDto>.Failure(
                "The financial statements template check only applies to a workbook. Accept Excel (.xlsx) on this " +
                "slot, or turn the template check off.");

        var result = RhshfDocumentRequirement.Create(
            request.Category, request.Title, request.Description, request.IsMandatory, request.SortOrder,
            request.AllowedFormats, request.MustMatchFinancialTemplate);
        if (result.IsFailure)
            return ApplicationResult<RhshfDocumentRequirementDto>.Failure(result.Error);

        await _repo.AddAsync(result.Value, ct);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfDocumentRequirementDto>.Success(MapToDto(result.Value));
    }

    internal static RhshfDocumentRequirementDto MapToDto(RhshfDocumentRequirement r) => new(
        r.Id, r.Category, r.Title, r.Description, r.IsMandatory, r.SortOrder, r.IsActive,
        r.AllowedFormatList, r.MustMatchFinancialTemplate);
}

public record UpdateRhshfDocumentRequirementCommand(
    Guid Id, string Title, string? Description, bool IsMandatory, int SortOrder,
    IReadOnlyList<RhshfDocumentFormat>? AllowedFormats = null,
    bool MustMatchFinancialTemplate = false)
    : IRequest<ApplicationResult<RhshfDocumentRequirementDto>>;

public class UpdateRhshfDocumentRequirementHandler
    : IRequestHandler<UpdateRhshfDocumentRequirementCommand, ApplicationResult<RhshfDocumentRequirementDto>>
{
    private readonly IRhshfDocumentRequirementRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateRhshfDocumentRequirementHandler(IRhshfDocumentRequirementRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfDocumentRequirementDto>> Handle(
        UpdateRhshfDocumentRequirementCommand request, CancellationToken ct = default)
    {
        var requirement = await _repo.GetByIdAsync(request.Id, ct);
        if (requirement is null)
            return ApplicationResult<RhshfDocumentRequirementDto>.Failure("Document requirement not found.");

        // Category is deliberately not editable: already-submitted cases were judged against it,
        // and repointing it would silently rewrite what those cases were required to provide.
        var result = requirement.Update(request.Title, request.Description, request.IsMandatory, request.SortOrder);
        if (result.IsFailure)
            return ApplicationResult<RhshfDocumentRequirementDto>.Failure(result.Error);

        // A format rule only binds a workbook, so asking for the financial-statements template
        // without accepting spreadsheets would store a rule that can never fire — say so rather
        // than saving something that looks set and does nothing.
        var formats = request.AllowedFormats ?? [];
        if (request.MustMatchFinancialTemplate && !formats.Contains(RhshfDocumentFormat.Spreadsheet))
            return ApplicationResult<RhshfDocumentRequirementDto>.Failure(
                "The financial statements template check only applies to a workbook. Accept Excel (.xlsx) on this " +
                "slot, or turn the template check off.");

        requirement.SetFormatRules(formats, request.MustMatchFinancialTemplate);

        _repo.Update(requirement);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfDocumentRequirementDto>.Success(
            CreateRhshfDocumentRequirementHandler.MapToDto(requirement));
    }
}

public record ToggleRhshfDocumentRequirementCommand(Guid Id, bool Activate)
    : IRequest<ApplicationResult<RhshfDocumentRequirementDto>>;

public class ToggleRhshfDocumentRequirementHandler
    : IRequestHandler<ToggleRhshfDocumentRequirementCommand, ApplicationResult<RhshfDocumentRequirementDto>>
{
    private readonly IRhshfDocumentRequirementRepository _repo;
    private readonly IUnitOfWork _uow;

    public ToggleRhshfDocumentRequirementHandler(IRhshfDocumentRequirementRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<RhshfDocumentRequirementDto>> Handle(
        ToggleRhshfDocumentRequirementCommand request, CancellationToken ct = default)
    {
        var requirement = await _repo.GetByIdAsync(request.Id, ct);
        if (requirement is null)
            return ApplicationResult<RhshfDocumentRequirementDto>.Failure("Document requirement not found.");

        // Deactivated rather than deleted — a case submitted last season must stay explicable
        // against the rules that were in force when it was submitted.
        if (request.Activate) requirement.Activate(); else requirement.Deactivate();

        _repo.Update(requirement);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<RhshfDocumentRequirementDto>.Success(
            CreateRhshfDocumentRequirementHandler.MapToDto(requirement));
    }
}
