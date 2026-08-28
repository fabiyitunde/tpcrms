using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;
using static CRMS.Application.Rhshf.RhshfStatusMapper;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// §4.1 of the integration brief. Idempotent on SubmissionId: re-submitting the same id returns
/// the same case with a freshly-issued token, never a duplicate case. (The brief's "same
/// case/token" wording is read as "never a duplicate case" being the hard requirement; the token
/// itself is safe to reissue since tokens are short-lived by design — see design doc §6 #5/#7.)
/// </summary>
public record SubmitConsolidatedEopCommand(SubmitConsolidatedEopRequest Request, string RawPayload)
    : IRequest<ApplicationResult<SubmitConsolidatedEopResultDto>>;

public class SubmitConsolidatedEopHandler
    : IRequestHandler<SubmitConsolidatedEopCommand, ApplicationResult<SubmitConsolidatedEopResultDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfTokenService _tokenService;
    private readonly IFineractDirectService _fineract;
    private readonly ILocationRepository _locationRepo;
    private readonly IUnitOfWork _uow;

    public SubmitConsolidatedEopHandler(
        IRhshfCreditProfileRepository repo,
        IRhshfTokenService tokenService,
        IFineractDirectService fineract,
        ILocationRepository locationRepo,
        IUnitOfWork uow)
    {
        _repo = repo;
        _tokenService = tokenService;
        _fineract = fineract;
        _locationRepo = locationRepo;
        _uow = uow;
    }

    public async Task<ApplicationResult<SubmitConsolidatedEopResultDto>> Handle(
        SubmitConsolidatedEopCommand request, CancellationToken ct = default)
    {
        var req = request.Request;

        var existing = await _repo.GetBySubmissionIdAsync(req.SubmissionId, ct);
        if (existing is not null)
        {
            var existingResponse = IssueTokenAndBuildResponse(existing);
            await _uow.SaveChangesAsync(ct);
            return ApplicationResult<SubmitConsolidatedEopResultDto>.Success(existingResponse);
        }

        var eopLines = req.EopLines?.Select(l => (l.Commodity, l.QuantityKg, l.UnitPricePerKg, l.LineValue));

        var result = RhshfCreditProfile.Create(
            submissionId: req.SubmissionId,
            programmeCode: req.Programme.Code,
            programmeName: req.Programme.Name,
            sessionCode: req.Session.Code,
            sessionName: req.Session.Name,
            facId: req.Fac.FacId,
            companyName: req.Fac.CompanyName,
            rcNumber: req.Fac.RcNumber,
            tin: req.Fac.Tin,
            boaAccountNumber: req.Fac.BoaAccountNumber,
            contactEmail: req.Fac.Contact?.Email ?? string.Empty,
            contactPhone: req.Fac.Contact?.Phone ?? string.Empty,
            state: req.Fac.State ?? string.Empty,
            lga: req.Fac.Lga ?? string.Empty,
            totalEopValue: req.TotalEopValue,
            currency: req.Currency,
            farmerCount: req.FarmerCount,
            callbackUrl: req.CallbackUrl,
            certifiedByAdmin: req.Metadata?.GetValueOrDefault("certifiedByAdmin"),
            certifiedAt: TryParseDate(req.Metadata?.GetValueOrDefault("certifiedAt")),
            rawSubmissionPayload: request.RawPayload,
            eopLines: eopLines,
            resolvedBranchId: null,
            resolvedOfficeId: null);

        if (result.IsFailure)
            return ApplicationResult<SubmitConsolidatedEopResultDto>.Failure(result.Error);

        var profile = result.Value;
        await ResolveBranchAsync(profile, req.Fac.BoaAccountNumber, ct);
        await _repo.AddAsync(profile, ct);
        var response = IssueTokenAndBuildResponse(profile);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<SubmitConsolidatedEopResultDto>.Success(response);
    }

    /// <summary>Best-effort branch resolution (design doc §3.1) — mirrors the algorithm
    /// NampWebhookController uses (BOA account -> Fineract client -> office -> CRMS branch), calling
    /// the same generic IFineractDirectService/ILocationRepository directly. Unlike NAMP's webhook,
    /// a failure here does NOT fail the submission — it would put a live external round-trip in the
    /// portal-facing endpoint's critical path for something only needed later, for staff queue
    /// routing (Phase 4). BranchResolutionNote records why, for manual follow-up.</summary>
    private async Task ResolveBranchAsync(RhshfCreditProfile profile, string boaAccountNumber, CancellationToken ct)
    {
        var accountResult = await _fineract.GetNampBoaAccountAsync(boaAccountNumber, ct);
        if (accountResult.IsFailure)
        {
            profile.ResolveBranch(null, null, $"Could not verify BOA account: {accountResult.Error}");
            return;
        }

        var clientResult = await _fineract.GetClientByIdAsync(accountResult.Value.ClientId, ct);
        if (clientResult.IsFailure)
        {
            profile.ResolveBranch(null, null, $"Could not retrieve branch: {clientResult.Error}");
            return;
        }

        var branch = await _locationRepo.GetBranchByNameAsync(clientResult.Value.OfficeName, ct);
        if (branch is null)
        {
            profile.ResolveBranch(null, null,
                $"BOA account belongs to Fineract office '{clientResult.Value.OfficeName}' which does not match any active CRMS branch.");
            return;
        }

        profile.ResolveBranch(branch.Id, branch.ParentLocationId, null);
    }

    private SubmitConsolidatedEopResultDto IssueTokenAndBuildResponse(RhshfCreditProfile profile)
    {
        var issued = _tokenService.IssueToken(profile.Id, profile.Reference, profile.FacId, profile.ProgrammeCode);
        profile.IssueToken(issued.Jti, DateTime.UtcNow, issued.ExpiresAt);

        return new SubmitConsolidatedEopResultDto(
            Reference: profile.Reference,
            Token: issued.Token,
            ProfilingUrl: issued.ProfilingUrl,
            TokenExpiresAt: issued.ExpiresAt,
            Status: profile.Status.ToWireFormat());
    }

    private static DateTime? TryParseDate(string? value)
        => DateTime.TryParse(value, out var parsed) ? parsed : null;
}
