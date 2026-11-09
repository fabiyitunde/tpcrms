using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Constants;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Billing.Queries;

// ───────────────────────────── Dashboard ─────────────────────────────

public sealed record BillingCategoryTotalDto(BillingCategory Category, string DisplayName, int Count, decimal Amount, decimal? Cost);

public sealed record BillingMonthTotalDto(DateTime Month, string Label, decimal Total, decimal? Cost, IReadOnlyDictionary<BillingCategory, decimal> ByCategory);

public sealed record BillingServiceTotalDto(string Item, string DisplayName, BillingCategory Category, int Count, int Failed, decimal UnitPrice, decimal Amount, decimal? Cost);

public sealed record BillingFlowLoansDto(BillingFlow Flow, string DisplayName, int Received, int Booked, int Declined, int Cancelled, int Processing, decimal Fees);

public sealed record BillingDashboardDto(
    DateTime Month,
    string Currency,
    decimal DueThisMonth,
    decimal ThirdPartyDue,
    decimal PlatformDue,
    decimal? OwnCost,
    decimal? Margin,
    int CallsThisMonth,
    int FailedCallsThisMonth,
    int UnattributedThisMonth,
    IReadOnlyList<BillingCategoryTotalDto> ByCategory,
    IReadOnlyList<BillingMonthTotalDto> ByMonth,
    IReadOnlyList<BillingServiceTotalDto> ByService,
    IReadOnlyList<BillingFlowLoansDto> Loans,
    int ReconstructedRows,
    DateTime? LiveSince,
    bool MonthIsReconstructed,
    bool AnyRateSet);

/// <param name="Month">Any date in the month wanted; UTC.</param>
/// <param name="Flow">Narrows every figure to one flow.</param>
/// <param name="IncludeCost">Billing Managers see the vendor's own cost; SystemAdmin does not.</param>
public sealed record GetBillingDashboardQuery(DateTime Month, BillingFlow? Flow, bool IncludeCost) : IRequest<ApplicationResult<BillingDashboardDto>>;

public sealed class GetBillingDashboardHandler : IRequestHandler<GetBillingDashboardQuery, ApplicationResult<BillingDashboardDto>>
{
    public const int MonthsShown = 12;
    private readonly IBillingUsageRepository _usage;
    private readonly IBillingRateRepository _rates;

    public GetBillingDashboardHandler(IBillingUsageRepository usage, IBillingRateRepository rates)
    {
        _usage = usage;
        _rates = rates;
    }

    public async Task<ApplicationResult<BillingDashboardDto>> Handle(GetBillingDashboardQuery request, CancellationToken ct = default)
    {
        var month = BillingUsage.MonthOf(request.Month);
        var windowStart = month.AddMonths(-(MonthsShown - 1));
        var rates = await _rates.GetAllAsync(ct);
        var book = new BillingPriceBook(rates);
        var allCounts = await _usage.CountsAsync(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), month, request.Flow, ct);
        var inMonth = allCounts.Where(c => c.Month == month).ToList();
        var currency = rates.FirstOrDefault()?.Currency ?? "NGN";

        decimal Amount(BillingUsageCount c) => c.Count * book.Resolve(c.Item, c.Flow, c.Month).UnitPrice;
        decimal Cost(BillingUsageCount c) => c.Count * book.Resolve(c.Item, c.Flow, c.Month).UnitCost;

        var byCategory = inMonth
            .GroupBy(c => BillingItems.CategoryOf(c.Item))
            .Select(g => new BillingCategoryTotalDto(g.Key, BillingItems.CategoryDisplayName(g.Key), g.Sum(c => c.Count), g.Sum(Amount), request.IncludeCost ? g.Sum(Cost) : null))
            .OrderBy(c => c.Category)
            .ToList();

        var byMonth = new List<BillingMonthTotalDto>();
        for (var m = windowStart; m <= month; m = m.AddMonths(1))
        {
            var rows = allCounts.Where(c => c.Month == m).ToList();
            var perCategory = rows.GroupBy(c => BillingItems.CategoryOf(c.Item)).ToDictionary(g => g.Key, g => g.Sum(Amount));
            byMonth.Add(new BillingMonthTotalDto(m, m.ToString("MMM yyyy"), perCategory.Values.Sum(), request.IncludeCost ? rows.Sum(Cost) : null, perCategory));
        }

        var byService = inMonth
            .Where(c => BillingItems.CategoryOf(c.Item) != BillingCategory.Platform)
            .GroupBy(c => c.Item)
            .Select(g =>
            {
                var flow = request.Flow ?? g.Select(c => c.Flow).First();
                return new BillingServiceTotalDto(g.Key, BillingItems.DisplayNameOf(g.Key), BillingItems.CategoryOf(g.Key),
                    g.Sum(c => c.Count), g.Where(c => !c.Succeeded).Sum(c => c.Count),
                    book.Resolve(g.Key, flow, month).UnitPrice, g.Sum(Amount), request.IncludeCost ? g.Sum(Cost) : null);
            })
            .OrderByDescending(s => s.Amount).ThenByDescending(s => s.Count)
            .ToList();

        var loans = BillingItems.LoanFlows
            .Where(f => request.Flow is null || request.Flow == f)
            .Select(f =>
            {
                int InMonth(string item) => inMonth.Where(c => c.Flow == f && c.Item == item).Sum(c => c.Count);
                int AllTime(string item) => allCounts.Where(c => c.Flow == f && c.Item == item).Sum(c => c.Count);
                var processing = AllTime(BillingItems.LoanReceived) - AllTime(BillingItems.LoanBooked) - AllTime(BillingItems.LoanDeclined) - AllTime(BillingItems.LoanCancelled);
                var fees = inMonth.Where(c => c.Flow == f && BillingItems.CategoryOf(c.Item) == BillingCategory.Platform).Sum(Amount);
                return new BillingFlowLoansDto(f, BillingItems.FlowDisplayName(f), InMonth(BillingItems.LoanReceived), InMonth(BillingItems.LoanBooked),
                    InMonth(BillingItems.LoanDeclined), InMonth(BillingItems.LoanCancelled), Math.Max(0, processing), fees);
            })
            .ToList();

        var platformDue = byCategory.Where(c => c.Category == BillingCategory.Platform).Sum(c => c.Amount);
        var thirdPartyDue = byCategory.Where(c => c.Category != BillingCategory.Platform).Sum(c => c.Amount);
        var due = platformDue + thirdPartyDue;
        decimal? ownCost = request.IncludeCost ? byCategory.Sum(c => c.Cost ?? 0) : null;
        var calls = inMonth.Where(c => BillingItems.CategoryOf(c.Item) != BillingCategory.Platform).ToList();

        var liveSince = await _usage.FirstLiveAtAsync(ct);
        var reconstructed = await _usage.CountBySourceAsync(BillingUsageSource.Reconstructed, ct);

        return ApplicationResult<BillingDashboardDto>.Success(new BillingDashboardDto(
            month, currency, due, thirdPartyDue, platformDue, ownCost,
            ownCost is null ? null : due - ownCost,
            calls.Sum(c => c.Count),
            calls.Where(c => !c.Succeeded).Sum(c => c.Count),
            calls.Where(c => c.Flow == BillingFlow.Unattributed).Sum(c => c.Count),
            byCategory, byMonth, byService, loans,
            reconstructed, liveSince,
            liveSince is { } live && month < BillingUsage.MonthOf(live),
            rates.Count > 0));
    }
}

// ───────────────────────────── Charges drill-down ─────────────────────────────

public sealed record BillingUsageRowDto(
    Guid Id, DateTime OccurredAt, BillingFlow Flow, string FlowName, Guid? CaseId, string? CaseReference, string CaseLink,
    string Item, string ItemName, BillingCategory Category, string CategoryName, string? SubjectType, string? SubjectHint,
    bool Succeeded, string? Detail, string Host, BillingUsageSource Source, decimal UnitPrice, decimal? UnitCost);

public sealed record BillingUsagePageDto(IReadOnlyList<BillingUsageRowDto> Items, int Total, int Page, int PageSize, decimal PageAmount);

public sealed record SearchBillingUsageQuery(BillingUsageSearch Search, bool IncludeCost) : IRequest<ApplicationResult<BillingUsagePageDto>>;

public sealed class SearchBillingUsageHandler : IRequestHandler<SearchBillingUsageQuery, ApplicationResult<BillingUsagePageDto>>
{
    private readonly IBillingUsageRepository _usage;
    private readonly IBillingRateRepository _rates;

    public SearchBillingUsageHandler(IBillingUsageRepository usage, IBillingRateRepository rates)
    {
        _usage = usage;
        _rates = rates;
    }

    public async Task<ApplicationResult<BillingUsagePageDto>> Handle(SearchBillingUsageQuery request, CancellationToken ct = default)
    {
        var search = request.Search with
        {
            Month = request.Search.Month is { } m ? BillingUsage.MonthOf(DateTime.SpecifyKind(m, DateTimeKind.Utc)) : null,
            Page = Math.Max(1, request.Search.Page),
            PageSize = Math.Clamp(request.Search.PageSize, 1, 200),
        };
        var (items, total) = await _usage.SearchAsync(search, ct);
        var book = new BillingPriceBook(await _rates.GetAllAsync(ct));
        var rows = items.Select(u => ToRow(u, book, request.IncludeCost)).ToList();
        return ApplicationResult<BillingUsagePageDto>.Success(new BillingUsagePageDto(rows, total, search.Page, search.PageSize, rows.Sum(r => r.UnitPrice)));
    }

    public static BillingUsageRowDto ToRow(BillingUsage u, BillingPriceBook book, bool includeCost)
    {
        var rate = book.Resolve(u.Item, u.Flow, u.Month);
        return new BillingUsageRowDto(u.Id, u.OccurredAt, u.Flow, BillingItems.FlowDisplayName(u.Flow), u.CaseId, u.CaseReference, CaseLink(u.Flow, u.CaseId, u.CaseReference),
            u.Item, BillingItems.DisplayNameOf(u.Item), u.Category, BillingItems.CategoryDisplayName(u.Category), u.SubjectType, u.SubjectHint,
            u.Succeeded, u.Detail, u.Host, u.Source, rate.UnitPrice, includeCost ? rate.UnitCost : null);
    }

    /// <summary>Where the case opens in the intranet, or empty when there is nothing to open.</summary>
    public static string CaseLink(BillingFlow flow, Guid? caseId, string? reference) => flow switch
    {
        BillingFlow.Corporate when caseId is not null => $"/applications/{caseId}",
        BillingFlow.Namp when caseId is not null => $"/namp/{caseId}",
        BillingFlow.Rhshf when !string.IsNullOrEmpty(reference) => $"/rhshf/detail/{reference}",
        BillingFlow.PartnerApi => "/admin/customer-check-jobs",
        _ => string.Empty,
    };
}

// ───────────────────────────── Loans drill-down ─────────────────────────────

public sealed record BillingLoanRowDto(
    BillingFlow Flow, string FlowName, Guid CaseId, string Reference, string? ApplicantName, string CaseLink,
    DateTime ReceivedAt, BillingLoanOutcome Outcome, string OutcomeName, DateTime? OutcomeAt, decimal? Amount,
    int Calls, decimal CallsAmount, decimal FeesAmount, decimal Total, decimal? Cost);

public sealed record BillingLoanPageDto(IReadOnlyList<BillingLoanRowDto> Items, int Total, int Page, int PageSize);

public sealed record GetBillingLoansQuery(DateTime? Month, BillingFlow? Flow, BillingLoanOutcome? Outcome, string? Search, int Page, int PageSize, bool IncludeCost)
    : IRequest<ApplicationResult<BillingLoanPageDto>>;

public sealed class GetBillingLoansHandler : IRequestHandler<GetBillingLoansQuery, ApplicationResult<BillingLoanPageDto>>
{
    private readonly IBillingLoanSource _loans;
    private readonly IBillingUsageRepository _usage;
    private readonly IBillingRateRepository _rates;

    public GetBillingLoansHandler(IBillingLoanSource loans, IBillingUsageRepository usage, IBillingRateRepository rates)
    {
        _loans = loans;
        _usage = usage;
        _rates = rates;
    }

    public async Task<ApplicationResult<BillingLoanPageDto>> Handle(GetBillingLoansQuery request, CancellationToken ct = default)
    {
        var all = await _loans.AllLoansAsync(ct);
        IEnumerable<BillingLoanSnapshot> q = all;
        if (request.Flow is { } flow) q = q.Where(l => l.Flow == flow);
        if (request.Outcome is { } outcome) q = q.Where(l => l.Outcome == outcome);
        if (request.Month is { } m)
        {
            var month = BillingUsage.MonthOf(m);
            q = q.Where(l => BillingUsage.MonthOf(l.ReceivedAt) == month || (l.OutcomeAt is { } o && BillingUsage.MonthOf(o) == month));
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            q = q.Where(l => l.Reference.Contains(s, StringComparison.OrdinalIgnoreCase) || (l.ApplicantName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        var filtered = q.OrderByDescending(l => l.OutcomeAt ?? l.ReceivedAt).ToList();
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 200);
        var slice = filtered.Skip((page - 1) * size).Take(size).ToList();

        var book = new BillingPriceBook(await _rates.GetAllAsync(ct));
        var usage = (await _usage.ForCasesAsync(slice.Select(l => l.CaseId), ct)).ToLookup(u => u.CaseId!.Value);
        var rows = slice.Select(l =>
        {
            var rowsForCase = usage[l.CaseId].ToList();
            var calls = rowsForCase.Where(u => u.Category != BillingCategory.Platform).ToList();
            var fees = rowsForCase.Where(u => u.Category == BillingCategory.Platform).ToList();
            var callsAmount = calls.Sum(u => book.Resolve(u.Item, u.Flow, u.Month).UnitPrice);
            var feesAmount = fees.Sum(u => book.Resolve(u.Item, u.Flow, u.Month).UnitPrice);
            decimal? cost = request.IncludeCost ? rowsForCase.Sum(u => book.Resolve(u.Item, u.Flow, u.Month).UnitCost) : null;
            return new BillingLoanRowDto(l.Flow, BillingItems.FlowDisplayName(l.Flow), l.CaseId, l.Reference, l.ApplicantName,
                SearchBillingUsageHandler.CaseLink(l.Flow, l.CaseId, l.Reference), l.ReceivedAt, l.Outcome, BillingLoanClassifier.DisplayName(l.Outcome),
                l.OutcomeAt, l.Amount, calls.Count, callsAmount, feesAmount, callsAmount + feesAmount, cost);
        }).ToList();

        return ApplicationResult<BillingLoanPageDto>.Success(new BillingLoanPageDto(rows, filtered.Count, page, size));
    }
}

// ───────────────────────────── Rates ─────────────────────────────

public sealed record BillingRateHistoryDto(DateTime EffectiveFrom, decimal UnitPrice, decimal? UnitCost, string? Note, string SetBy, DateTime SetAt);

public sealed record BillingRateLineDto(string Item, string DisplayName, BillingCategory Category, string CategoryName, BillingFlow? Flow,
    decimal UnitPrice, decimal? UnitCost, DateTime? EffectiveFrom, bool IsSet, IReadOnlyList<BillingRateHistoryDto> History);

public sealed record BillingRateSheetDto(string Currency, IReadOnlyList<BillingRateLineDto> Services, IReadOnlyList<BillingRateLineDto> PlatformFees, IReadOnlyList<BillingFlow> LoanFlows);

public sealed record GetBillingRatesQuery(bool IncludeCost) : IRequest<ApplicationResult<BillingRateSheetDto>>;

public sealed class GetBillingRatesHandler : IRequestHandler<GetBillingRatesQuery, ApplicationResult<BillingRateSheetDto>>
{
    private readonly IBillingRateRepository _rates;

    public GetBillingRatesHandler(IBillingRateRepository rates) => _rates = rates;

    public async Task<ApplicationResult<BillingRateSheetDto>> Handle(GetBillingRatesQuery request, CancellationToken ct = default)
    {
        var rates = await _rates.GetAllAsync(ct);
        var book = new BillingPriceBook(rates);
        var currency = rates.FirstOrDefault()?.Currency ?? "NGN";

        BillingRateLineDto Line(BillingItem item, BillingFlow? flow)
        {
            var current = book.Current(item.Item, flow);
            var history = book.History(item.Item, flow)
                .Select(r => new BillingRateHistoryDto(r.EffectiveFrom, r.UnitPrice, request.IncludeCost ? r.UnitCost : null, r.Note, r.ModifiedBy ?? r.CreatedBy, r.ModifiedAt ?? r.CreatedAt))
                .ToList();
            return new BillingRateLineDto(item.Item, item.DisplayName, item.Category, BillingItems.CategoryDisplayName(item.Category), flow,
                current.UnitPrice, request.IncludeCost ? current.UnitCost : null, current.EffectiveFrom, current.IsSet, history);
        }

        var services = BillingItems.All.Where(i => !i.PerFlow).Select(i => Line(i, null)).ToList();
        var platform = BillingItems.All.Where(i => i.PerFlow).SelectMany(i => BillingItems.LoanFlows.Select(f => Line(i, f))).ToList();
        return ApplicationResult<BillingRateSheetDto>.Success(new BillingRateSheetDto(currency, services, platform, BillingItems.LoanFlows));
    }
}

// ───────────────────────────── Access ─────────────────────────────

public sealed record BillingManagerDto(Guid Id, string FullName, string Email, string Status, DateTime? LastLoginAt, bool HasOtherRoles);

public sealed record BillingLedgerStatusDto(int LiveRows, int DerivedRows, int ReconstructedRows, DateTime? LiveSince);

public sealed record GetBillingAccessQuery() : IRequest<ApplicationResult<(IReadOnlyList<BillingManagerDto> Managers, BillingLedgerStatusDto Ledger)>>;

public sealed class GetBillingAccessHandler : IRequestHandler<GetBillingAccessQuery, ApplicationResult<(IReadOnlyList<BillingManagerDto> Managers, BillingLedgerStatusDto Ledger)>>
{
    private readonly IUserRepository _users;
    private readonly IBillingUsageRepository _usage;

    public GetBillingAccessHandler(IUserRepository users, IBillingUsageRepository usage)
    {
        _users = users;
        _usage = usage;
    }

    public async Task<ApplicationResult<(IReadOnlyList<BillingManagerDto> Managers, BillingLedgerStatusDto Ledger)>> Handle(GetBillingAccessQuery request, CancellationToken ct = default)
    {
        var holders = await _users.GetByRoleAsync(Roles.BillingManager, ct);
        var managers = holders
            .OrderBy(u => u.FullName)
            .Select(u => new BillingManagerDto(u.Id, u.FullName, u.Email, u.Status.ToString(), u.LastLoginAt,
                u.UserRoles.Any(r => r.Role is not null && r.Role.Name != Roles.BillingManager)))
            .ToList();
        var ledger = new BillingLedgerStatusDto(
            await _usage.CountBySourceAsync(BillingUsageSource.Live, ct),
            await _usage.CountBySourceAsync(BillingUsageSource.Derived, ct),
            await _usage.CountBySourceAsync(BillingUsageSource.Reconstructed, ct),
            await _usage.FirstLiveAtAsync(ct));
        return ApplicationResult<(IReadOnlyList<BillingManagerDto>, BillingLedgerStatusDto)>.Success((managers, ledger));
    }
}
