using System.Text.Json;
using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Application.Namp.Queries;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Application.Namp.Commands;

// ── Fetch company details + directors from SmartComply CAC ─────────────────

public record FetchNampCacDetailsCommand(
    Guid NampApplicationId,
    Guid UserId,
    bool ForceRefresh = false
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class FetchNampCacDetailsHandler
    : IRequestHandler<FetchNampCacDetailsCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly ISmartComplyProvider _smartComply;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<FetchNampCacDetailsHandler> _logger;

    public FetchNampCacDetailsHandler(
        INampApplicationRepository repo,
        ISmartComplyProvider smartComply,
        IUnitOfWork uow,
        ILogger<FetchNampCacDetailsHandler> logger)
    {
        _repo = repo;
        _smartComply = smartComply;
        _uow = uow;
        _logger = logger;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        FetchNampCacDetailsCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        if (app.ApplicantCategory != NampApplicantCategory.AgroServiceCompany)
            return ApplicationResult<NampApplicationDto>.Failure("CAC lookup applies only to Agro-Service company applicants.");

        if (string.IsNullOrWhiteSpace(app.RcNumber))
            return ApplicationResult<NampApplicationDto>.Failure("An RC number is required to fetch CAC details.");

        // Cost guard: don't re-bill SmartComply if we already have CAC data, unless explicitly refreshing.
        if (app.CacFetchedAt.HasValue && !request.ForceRefresh)
            return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(app));

        var companyType = DeriveCompanyType(app.RcNumber);
        var result = await _smartComply.VerifyCacAdvancedAsync(app.RcNumber.Trim(), app.CompanyName ?? app.ApplicantName, companyType, ct);
        if (result.IsFailure)
            return ApplicationResult<NampApplicationDto>.Failure(result.Error ?? "CAC lookup failed.");

        var cac = result.Value;

        app.SetCacCompanyProfile(
            cac.Status,
            cac.CompanyType,
            cac.RegistrationDate,
            cac.NatureOfBusiness,
            cac.ShareCapital,
            cac.CompanyId,
            cac.Address,
            cac.City,
            cac.State,
            JsonSerializer.Serialize(cac));

        // Track which directors the current CAC response contains, so stale ones can be pruned below.
        var seenCacIds = new HashSet<long>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in cac.Directors)
        {
            var fullName = !string.IsNullOrWhiteSpace(d.FullName)
                ? d.FullName!
                : $"{d.FirstName} {d.OtherName} {d.Surname}".Replace("  ", " ").Trim();

            if (d.Id.HasValue) seenCacIds.Add(d.Id.Value);
            else seenNames.Add(fullName);

            var existing = app.FindCacDirector(d.Id, fullName);
            if (existing is not null)
            {
                existing.RefreshCacFields(
                    fullName, d.Surname, d.FirstName, d.OtherName, d.Gender, d.DateOfBirth, d.Nationality,
                    d.Occupation, d.Email, d.PhoneNumber, d.Address, d.City, d.State,
                    d.IsChairman ?? false, d.DateOfAppointment, d.AffiliateType, d.Status,
                    d.TypeOfShares, d.NumSharesAlloted, d.IdentityNumber);
            }
            else
            {
                var director = NampDirector.FromCac(
                    app.Id, d.Id, fullName, d.Surname, d.FirstName, d.OtherName, d.Gender, d.DateOfBirth,
                    d.Nationality, d.Occupation, d.Email, d.PhoneNumber, d.Address, d.City, d.State,
                    d.IsChairman ?? false, d.DateOfAppointment, d.AffiliateType, d.Status,
                    d.TypeOfShares, d.NumSharesAlloted, d.IdentityNumber);
                await _repo.AddDirectorAsync(director, ct);
            }
        }

        // Prune CAC-sourced directors that are no longer in the CAC response (e.g. leftover test
        // data, or a director who has left the company). Manually-added directors are always kept.
        var stale = app.Directors
            .Where(existing => existing.SourcedFromCac &&
                (existing.CacDirectorId.HasValue
                    ? !seenCacIds.Contains(existing.CacDirectorId.Value)
                    : !seenNames.Contains(existing.FullName)))
            .ToList();
        foreach (var s in stale)
            _repo.RemoveDirector(s);

        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        var refreshed = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(refreshed!));
    }

    private static string DeriveCompanyType(string rcNumber)
    {
        var rc = rcNumber.Trim().ToUpperInvariant();
        if (rc.StartsWith("BN")) return "BN";
        if (rc.StartsWith("IT")) return "IT";
        return "RC";
    }
}

// ── Add a director/shareholder manually ────────────────────────────────────

public record AddNampDirectorCommand(
    Guid NampApplicationId,
    Guid UserId,
    string FullName,
    string? Bvn,
    decimal? ShareholdingPercent,
    bool IsChairman,
    string? Email,
    string? PhoneNumber
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class AddNampDirectorHandler
    : IRequestHandler<AddNampDirectorCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public AddNampDirectorHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        AddNampDirectorCommand request, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        if (app is null) return ApplicationResult<NampApplicationDto>.Failure("NAMP application not found.");

        var result = NampDirector.CreateManual(
            app.Id, request.FullName, request.Bvn, request.ShareholdingPercent,
            request.IsChairman, request.Email, request.PhoneNumber);
        if (result.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(result.Error);

        await _repo.AddDirectorAsync(result.Value, ct);
        app.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        var refreshed = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(refreshed!));
    }
}

// ── Update a director's manually-completed fields (BVN, shareholding) ───────

public record UpdateNampDirectorCommand(
    Guid NampApplicationId,
    Guid DirectorId,
    Guid UserId,
    string? Bvn,
    decimal? ShareholdingPercent
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class UpdateNampDirectorHandler
    : IRequestHandler<UpdateNampDirectorCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdateNampDirectorHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        UpdateNampDirectorCommand request, CancellationToken ct = default)
    {
        var director = await _repo.GetDirectorByIdAsync(request.DirectorId, ct);
        if (director is null || director.NampApplicationId != request.NampApplicationId)
            return ApplicationResult<NampApplicationDto>.Failure("Director not found.");

        var bvnResult = director.UpdateBvn(request.Bvn);
        if (bvnResult.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(bvnResult.Error);

        var shareResult = director.UpdateShareholding(request.ShareholdingPercent);
        if (shareResult.IsFailure) return ApplicationResult<NampApplicationDto>.Failure(shareResult.Error);

        director.SetAuditInfo(request.UserId.ToString());
        await _uow.SaveChangesAsync(ct);

        var refreshed = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(refreshed!));
    }
}

// ── Remove a director ───────────────────────────────────────────────────────

public record RemoveNampDirectorCommand(
    Guid NampApplicationId,
    Guid DirectorId,
    Guid UserId
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class RemoveNampDirectorHandler
    : IRequestHandler<RemoveNampDirectorCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampApplicationRepository _repo;
    private readonly IUnitOfWork _uow;

    public RemoveNampDirectorHandler(INampApplicationRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        RemoveNampDirectorCommand request, CancellationToken ct = default)
    {
        var director = await _repo.GetDirectorByIdAsync(request.DirectorId, ct);
        if (director is null || director.NampApplicationId != request.NampApplicationId)
            return ApplicationResult<NampApplicationDto>.Failure("Director not found.");

        _repo.RemoveDirector(director);
        await _uow.SaveChangesAsync(ct);

        var refreshed = await _repo.GetByIdWithDetailsAsync(request.NampApplicationId, ct);
        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(refreshed!));
    }
}
