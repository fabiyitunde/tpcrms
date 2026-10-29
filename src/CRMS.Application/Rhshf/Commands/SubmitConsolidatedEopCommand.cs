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
    private readonly IRhshfScheduleFarmerRepository _schedule;

    public SubmitConsolidatedEopHandler(
        IRhshfCreditProfileRepository repo,
        IRhshfTokenService tokenService,
        IFineractDirectService fineract,
        ILocationRepository locationRepo,
        IUnitOfWork uow,
        IRhshfScheduleFarmerRepository schedule)
    {
        _repo = repo;
        _tokenService = tokenService;
        _fineract = fineract;
        _locationRepo = locationRepo;
        _uow = uow;
        _schedule = schedule;
    }

    public async Task<ApplicationResult<SubmitConsolidatedEopResultDto>> Handle(
        SubmitConsolidatedEopCommand request, CancellationToken ct = default)
    {
        var req = request.Request;

        var existing = await _repo.GetBySubmissionIdAsync(req.SubmissionId, ct);
        if (existing is not null)
        {
            // A repeat is otherwise answered with the case as it stands, but a breakdown the case
            // never received is taken now: ratification is refused without one, and this is the
            // only way the portal can supply it after the fact. One already held is never replaced,
            // and past offer acceptance the repeat stays the plain idempotent answer it always was —
            // a retry must not start failing. A breakdown that does not reconcile is refused, as it
            // would be on a first submission.
            if (req.Financing is not null && existing.CanTakeLateFinancing)
            {
                var attached = existing.AttachLateFinancing(MapFinancing(req.Financing)!);
                if (attached.IsFailure)
                    return ApplicationResult<SubmitConsolidatedEopResultDto>.Failure(attached.Error);
            }

            // The farmer list is taken at any stage, even after acceptance: it changes no amount and
            // no decision, and it is the only way cases submitted before the portal sent it (Grandbai
            // among them) can be found by the party-exposure lookup. Taken once, never replaced.
            if (req.FarmerSchedule is { Count: > 0 }
                && await _schedule.CountForProfileAsync(existing.Id, ct) == 0)
            {
                var scheduled = await AddScheduleAsync(existing, req.FarmerSchedule, ct);
                if (scheduled.IsFailure)
                    return ApplicationResult<SubmitConsolidatedEopResultDto>.Failure(scheduled.Error);
            }

            var existingResponse = IssueTokenAndBuildResponse(existing);
            await _uow.SaveChangesAsync(ct);
            return ApplicationResult<SubmitConsolidatedEopResultDto>.Success(existingResponse);
        }

        var eopLines = req.EopLines?.Select(l => new RhshfEopLineInput(
            l.Commodity, l.QuantityKg, l.UnitPricePerKg, l.LineValue,
            l.Supplier, l.Product, l.UnitOfMeasurement));

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
            resolvedOfficeId: null,
            financing: MapFinancing(req.Financing));

        if (result.IsFailure)
            return ApplicationResult<SubmitConsolidatedEopResultDto>.Failure(result.Error);

        var profile = result.Value;
        if (req.FarmerSchedule is { Count: > 0 })
        {
            var scheduled = await AddScheduleAsync(profile, req.FarmerSchedule, ct);
            if (scheduled.IsFailure)
                return ApplicationResult<SubmitConsolidatedEopResultDto>.Failure(scheduled.Error);
        }
        await ResolveBranchAsync(profile, req.Fac.BoaAccountNumber, ct);
        await _repo.AddAsync(profile, ct);
        var response = IssueTokenAndBuildResponse(profile);
        await _uow.SaveChangesAsync(ct);

        return ApplicationResult<SubmitConsolidatedEopResultDto>.Success(response);
    }

    /// <summary>Checks the farmer list against the case's own EOPs and queues its rows to be saved
    /// with the case. Refused whole if any entry is wrong.</summary>
    private async Task<CRMS.Domain.Common.Result> AddScheduleAsync(
        RhshfCreditProfile profile, List<RhshfScheduleFarmerDto> schedule, CancellationToken ct)
    {
        var knownEops = profile.FinancingEops.Select(e => e.EopId)
            .Concat(profile.FinancingLines.Select(l => l.EopId))
            .OfType<Guid>()
            .ToHashSet();
        var built = RhshfScheduleFarmer.BuildSchedule(
            profile.Id,
            schedule.Select(f => new RhshfScheduleFarmerInput(f?.EopId, f?.FarmerRef, f?.Bvn, f?.Nin, f?.Nuban)).ToList(),
            knownEops);
        if (built.IsFailure)
            return CRMS.Domain.Common.Result.Failure(built.Error);

        await _schedule.AddRangeAsync(built.Value, ct);
        return CRMS.Domain.Common.Result.Success();
    }

    /// <summary>
    /// Maps the portal's financing block, treating absence as "not sent yet" rather than as empty.
    /// <para>
    /// Amounts are carried across verbatim. The portal's spec is explicit that they must not be
    /// recomputed from quantity × unit price or rate × base: supplier-pack unit prices are rounded
    /// down to the kobo and the per-farmer charges are rounded before being multiplied out, so a
    /// recomputation lands a few kobo away from the figure the borrower signs against.
    /// </para>
    /// </summary>
    private static RhshfFinancingInput? MapFinancing(RhshfFinancingDto? financing)
    {
        if (financing is null)
            return null;

        var lines = (financing.InputLines ?? [])
            .Select(l => new RhshfFinancingLineInput(
                l.EopId, l.Crop, l.Item ?? string.Empty, l.Category, l.SpecificInput, l.Supplier,
                l.QuantityPerFarmer, l.Unit, l.UnitPrice, l.Basis, l.Farmers,
                l.Amount ?? 0m, l.PackagingExcess ?? 0m,
                // Absent means financed: the programme finances everything today, and the
                // reconciliation rules would catch it at once if that stopped being true.
                l.InLoan ?? true,
                new RhshfFinancingLineDetail(
                    l.BaseUnit, l.PackSize, l.PackUnit, l.PackCount, l.PricePerPack, l.ProcurementQuantity,
                    l.PackagingExcessQuantity, l.PriceVersionId, l.SupplierProductId,
                    l.ServiceProvider?.ProviderId, l.ServiceProvider?.RegisteredName, l.ServiceProvider?.ServiceType,
                    l.ServiceProvider?.Rate, l.ServiceProvider?.RateBasis, l.ServiceProvider?.RateVersion,
                    l.ServiceProvider?.BillableQuantity, l.ServiceProvider?.AssignmentId)))
            .ToList();

        var charges = (financing.Charges ?? [])
            .Select(c => new RhshfFinancingChargeInput(
                c.Code ?? string.Empty, c.Label ?? string.Empty, c.Rate, c.RateBasis,
                c.Amount ?? 0m, c.InLoan ?? true))
            .ToList();

        return new RhshfFinancingInput(
            lines, charges, financing.InputsTotal, financing.PackagingExcessTotal,
            financing.ChargesTotal, financing.ApprovedEopTotal, financing.LoanAmount,
            financing.ExcludedFromLoan, MapEops(financing.Eops));
    }

    /// <summary>The per-EOP summary. Sent from the start but not read until CRMSQA#2 — it is the only
    /// place the portal states each EOP's own farmer count, and the only source for per-EOP subtotals
    /// of the charges. Public so the startup back-fill reads stored submissions the same way.</summary>
    public static List<RhshfFinancingEopInput>? MapEops(List<RhshfFinancingEopDto>? eops) =>
        eops?.Select(e => new RhshfFinancingEopInput(
                e.EopId, e.Crop, e.State, e.Farmers, e.Inputs ?? 0m, e.PackagingExcess ?? 0m,
                e.Insurance ?? 0m, e.WorkingCapital ?? 0m, e.TechServicesFee ?? 0m,
                e.ApprovedTotal ?? 0m, e.LoanAmount ?? 0m,
                e.EopReference, e.ApprovedAt, e.Revision, e.SnapshotHash))
            .ToList();

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
                $"BOA account belongs to core banking office '{clientResult.Value.OfficeName}' which does not match any active CRMS branch.");
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
