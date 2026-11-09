using CRMS.Application.Billing.Commands;
using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Billing;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Services;

namespace CRMS.Application.Billing.Statements;

// ───────────────────────────── DTOs ─────────────────────────────

public sealed record BillingStatementLineDto(DateTime Month, string Item, string ItemName, BillingCategory Category, string CategoryName, BillingFlow Flow, string FlowName, int Quantity, decimal UnitPrice, decimal Amount, decimal? UnitCost);

public sealed record BillingStatementDto(
    Guid Id, string Number, DateTime Month, BillingStatementStatus Status, string Currency,
    decimal Subtotal, decimal VatRatePercent, decimal VatAmount, decimal Total, decimal? CostTotal, int UsageCount,
    DateTime IssuedAt, string IssuedByName, DateTime DueDate, string? Notes,
    DateTime? PaidAt, string? PaymentReference, string? PaymentNote,
    DateTime? VoidedAt, string? VoidReason,
    IReadOnlyList<BillingStatementLineDto> Lines);

/// <summary>What a statement for a month would contain if issued now.</summary>
public sealed record BillingStatementPreviewDto(
    DateTime Month, string Currency, decimal Subtotal, decimal VatRatePercent, decimal VatAmount, decimal Total, decimal? CostTotal, int UsageCount,
    IReadOnlyList<BillingStatementLineDto> Lines, IReadOnlyList<string> Warnings, bool CanIssue, string? BlockedReason);

public sealed record BillingStatementFile(string FileName, string ContentType, byte[] Content);

/// <summary>Renders a statement to a file. Implemented in Infrastructure.</summary>
public interface IBillingStatementExporter
{
    byte[] ToPdf(BillingStatementDto statement, BillingSettings settings);
    byte[] ToExcel(BillingStatementDto statement, BillingSettings settings);
}

internal static class StatementMapping
{
    public static BillingStatementLineDto Line(BillingStatementLine l, bool includeCost) =>
        new(l.Month, l.Item, l.ItemName, l.Category, BillingItems.CategoryDisplayName(l.Category), l.Flow, BillingItems.FlowDisplayName(l.Flow),
            l.Quantity, l.UnitPrice, l.Amount, includeCost ? l.UnitCost : null);

    public static BillingStatementDto Dto(BillingStatement s, BillingSettings settings, bool includeCost) =>
        new(s.Id, s.Number, s.Month, s.Status, s.Currency, s.Subtotal, s.VatRatePercent, s.VatAmount, s.Total, includeCost ? s.CostTotal : null, s.UsageCount,
            s.IssuedAt, s.IssuedByName, s.IssuedAt.AddDays(settings.PaymentTermsDays), s.Notes,
            s.PaidAt, s.PaymentReference, s.PaymentNote, s.VoidedAt, s.VoidReason,
            s.Lines.OrderBy(l => l.SortOrder).Select(l => Line(l, includeCost)).ToList());

    /// <summary>Turns grouped counts into priced lines: earlier months first, then by flow, category and item.</summary>
    public static List<(DateTime Month, string Item, BillingFlow Flow, int Quantity, BillingUnitRate Rate)> PriceLines(IEnumerable<BillingUsageCount> counts, BillingPriceBook book) =>
        counts.GroupBy(c => (c.Month, c.Item, c.Flow))
            .Select(g => (g.Key.Month, g.Key.Item, g.Key.Flow, Quantity: g.Sum(c => c.Count), Rate: book.Resolve(g.Key.Item, g.Key.Flow, g.Key.Month)))
            .OrderBy(l => l.Month).ThenBy(l => l.Flow).ThenBy(l => BillingItems.CategoryOf(l.Item)).ThenBy(l => BillingItems.DisplayNameOf(l.Item))
            .ToList();
}

// ───────────────────────────── Preview ─────────────────────────────

public sealed record PreviewBillingStatementQuery(DateTime Month, bool IncludeCost) : IRequest<ApplicationResult<BillingStatementPreviewDto>>;

public sealed class PreviewBillingStatementHandler : IRequestHandler<PreviewBillingStatementQuery, ApplicationResult<BillingStatementPreviewDto>>
{
    private readonly IBillingUsageRepository _usage;
    private readonly IBillingRateRepository _rates;
    private readonly IBillingStatementRepository _statements;
    private readonly BillingSettings _settings;

    public PreviewBillingStatementHandler(IBillingUsageRepository usage, IBillingRateRepository rates, IBillingStatementRepository statements, BillingSettings settings)
    {
        _usage = usage;
        _rates = rates;
        _statements = statements;
        _settings = settings;
    }

    public async Task<ApplicationResult<BillingStatementPreviewDto>> Handle(PreviewBillingStatementQuery request, CancellationToken ct = default)
    {
        var month = BillingUsage.MonthOf(DateTime.SpecifyKind(request.Month, DateTimeKind.Utc));
        var book = new BillingPriceBook(await _rates.GetAllAsync(ct));
        var lines = StatementMapping.PriceLines(await _usage.UnbilledCountsAsync(month, ct), book);
        var warnings = new List<string>();
        string? blocked = null;

        var thisMonth = BillingUsage.MonthOf(DateTime.UtcNow);
        if (month > thisMonth)
            blocked = "That month has not started.";
        else if (await _statements.ActiveForMonthAsync(month, ct) is { } existing)
            blocked = $"Statement {existing.Number} already covers {month:MMMM yyyy}. Void it first if it must be reissued.";
        else if (lines.Count == 0)
            blocked = "There is nothing unbilled up to this month.";
        if (month == thisMonth)
            warnings.Add("The month has not ended. Calls made after issue will roll into the next statement.");
        var earlier = lines.Where(l => l.Month < month).Sum(l => l.Quantity);
        if (earlier > 0)
            warnings.Add($"{earlier:N0} rows from earlier months were never billed and are included as their own lines.");
        var unpriced = lines.Where(l => !l.Rate.IsSet).Select(l => BillingItems.DisplayNameOf(l.Item)).Distinct().ToList();
        if (unpriced.Count > 0)
            warnings.Add($"No price set, so billed at zero: {string.Join(", ", unpriced)}.");

        var dtoLines = lines.Select(l => new BillingStatementLineDto(l.Month, l.Item, BillingItems.DisplayNameOf(l.Item), BillingItems.CategoryOf(l.Item),
            BillingItems.CategoryDisplayName(BillingItems.CategoryOf(l.Item)), l.Flow, BillingItems.FlowDisplayName(l.Flow), l.Quantity, l.Rate.UnitPrice,
            decimal.Round(l.Quantity * l.Rate.UnitPrice, 2), request.IncludeCost ? l.Rate.UnitCost : null)).ToList();
        var subtotal = dtoLines.Sum(l => l.Amount);
        var vat = decimal.Round(subtotal * _settings.VatRatePercent / 100m, 2);
        decimal? cost = request.IncludeCost ? lines.Sum(l => l.Quantity * l.Rate.UnitCost) : null;

        return ApplicationResult<BillingStatementPreviewDto>.Success(new BillingStatementPreviewDto(
            month, _settings.Currency, subtotal, _settings.VatRatePercent, vat, subtotal + vat, cost, lines.Sum(l => l.Quantity),
            dtoLines, warnings, blocked is null, blocked));
    }
}

// ───────────────────────────── Issue ─────────────────────────────

public sealed record IssueBillingStatementCommand(DateTime Month, string? Notes, Guid ActorUserId, string ActorName) : IRequest<ApplicationResult<BillingStatementDto>>;

public sealed class IssueBillingStatementHandler : IRequestHandler<IssueBillingStatementCommand, ApplicationResult<BillingStatementDto>>
{
    private readonly IBillingUsageRepository _usage;
    private readonly IBillingRateRepository _rates;
    private readonly IBillingStatementRepository _statements;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly AuditService _audit;
    private readonly BillingSettings _settings;

    public IssueBillingStatementHandler(IBillingUsageRepository usage, IBillingRateRepository rates, IBillingStatementRepository statements,
        IUserRepository users, IUnitOfWork uow, AuditService audit, BillingSettings settings)
    {
        _usage = usage;
        _rates = rates;
        _statements = statements;
        _users = users;
        _uow = uow;
        _audit = audit;
        _settings = settings;
    }

    public static string NumberFor(string prefix, DateTime month, int sequence) => $"{prefix}-{month:yyyyMM}-{sequence:00}";

    public async Task<ApplicationResult<BillingStatementDto>> Handle(IssueBillingStatementCommand request, CancellationToken ct = default)
    {
        var access = await BillingAccess.RequireManagerAsync(_users, request.ActorUserId, ct);
        if (!access.IsSuccess) return ApplicationResult<BillingStatementDto>.Failure(access.Error!);

        var month = BillingUsage.MonthOf(DateTime.SpecifyKind(request.Month, DateTimeKind.Utc));
        if (month > BillingUsage.MonthOf(DateTime.UtcNow))
            return ApplicationResult<BillingStatementDto>.Failure("That month has not started.");
        if (await _statements.ActiveForMonthAsync(month, ct) is { } existing)
            return ApplicationResult<BillingStatementDto>.Failure($"Statement {existing.Number} already covers {month:MMMM yyyy}.");

        if ((await _usage.UnbilledCountsAsync(month, ct)).Count == 0)
            return ApplicationResult<BillingStatementDto>.Failure("There is nothing unbilled up to this month.");

        var sequence = await _statements.CountForMonthAsync(month, ct) + 1;
        var created = BillingStatement.Create(NumberFor(_settings.StatementPrefix, month, sequence), month, _settings.Currency, _settings.VatRatePercent,
            request.ActorUserId, request.ActorName, request.Notes);
        if (created.IsFailure) return ApplicationResult<BillingStatementDto>.Failure(created.Error);
        var statement = created.Value;

        // The statement row exists before any usage row points at it, so a failure part-way leaves a
        // visible, voidable statement rather than rows stamped with a number nobody can find. Rows are
        // stamped before they are counted: a call landing in between belongs to the next statement
        // rather than being counted here without being marked.
        await _statements.AddAsync(statement, ct);
        await _uow.SaveChangesAsync(ct);
        await _usage.AssignUnbilledAsync(month, statement.Id, ct);
        var book = new BillingPriceBook(await _rates.GetAllAsync(ct));
        foreach (var l in StatementMapping.PriceLines(await _usage.CountsForStatementAsync(statement.Id, ct), book))
            statement.AddLine(l.Month, l.Item, BillingItems.DisplayNameOf(l.Item), BillingItems.CategoryOf(l.Item), l.Flow, l.Quantity, l.Rate.UnitPrice, l.Rate.UnitCost);
        await _statements.UpdateAsync(statement, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.Create, AuditCategory.System,
            $"Billing statement {statement.Number} issued for {month:MMMM yyyy}: {statement.UsageCount:N0} rows, {statement.Currency} {statement.Total:N2} including VAT.",
            "BillingStatement", statement.Id, statement.Number, request.ActorUserId, request.ActorName, ct: ct);

        return ApplicationResult<BillingStatementDto>.Success(StatementMapping.Dto(statement, _settings, includeCost: true));
    }
}

// ───────────────────────────── Read ─────────────────────────────

public sealed record BillingStatementSummaryDto(Guid Id, string Number, DateTime Month, BillingStatementStatus Status, string Currency, decimal Total, int UsageCount, DateTime IssuedAt, DateTime DueDate, DateTime? PaidAt, bool Overdue);

public sealed record GetBillingStatementsQuery() : IRequest<ApplicationResult<IReadOnlyList<BillingStatementSummaryDto>>>;

public sealed class GetBillingStatementsHandler : IRequestHandler<GetBillingStatementsQuery, ApplicationResult<IReadOnlyList<BillingStatementSummaryDto>>>
{
    private readonly IBillingStatementRepository _statements;
    private readonly BillingSettings _settings;

    public GetBillingStatementsHandler(IBillingStatementRepository statements, BillingSettings settings)
    {
        _statements = statements;
        _settings = settings;
    }

    public async Task<ApplicationResult<IReadOnlyList<BillingStatementSummaryDto>>> Handle(GetBillingStatementsQuery request, CancellationToken ct = default)
    {
        var all = await _statements.GetAllAsync(ct);
        var now = DateTime.UtcNow;
        var list = all.OrderByDescending(s => s.Month).ThenByDescending(s => s.IssuedAt).Select(s =>
        {
            var due = s.IssuedAt.AddDays(_settings.PaymentTermsDays);
            return new BillingStatementSummaryDto(s.Id, s.Number, s.Month, s.Status, s.Currency, s.Total, s.UsageCount, s.IssuedAt, due, s.PaidAt,
                s.Status == BillingStatementStatus.Issued && due < now);
        }).ToList();
        return ApplicationResult<IReadOnlyList<BillingStatementSummaryDto>>.Success(list);
    }
}

public sealed record GetBillingStatementQuery(Guid Id, bool IncludeCost) : IRequest<ApplicationResult<BillingStatementDto>>;

public sealed class GetBillingStatementHandler : IRequestHandler<GetBillingStatementQuery, ApplicationResult<BillingStatementDto>>
{
    private readonly IBillingStatementRepository _statements;
    private readonly BillingSettings _settings;

    public GetBillingStatementHandler(IBillingStatementRepository statements, BillingSettings settings)
    {
        _statements = statements;
        _settings = settings;
    }

    public async Task<ApplicationResult<BillingStatementDto>> Handle(GetBillingStatementQuery request, CancellationToken ct = default)
    {
        var s = await _statements.GetByIdAsync(request.Id, ct);
        return s is null
            ? ApplicationResult<BillingStatementDto>.Failure("Statement not found.")
            : ApplicationResult<BillingStatementDto>.Success(StatementMapping.Dto(s, _settings, request.IncludeCost));
    }
}

// ───────────────────────────── Export ─────────────────────────────

public sealed record ExportBillingStatementQuery(Guid Id, string Format) : IRequest<ApplicationResult<BillingStatementFile>>;

public sealed class ExportBillingStatementHandler : IRequestHandler<ExportBillingStatementQuery, ApplicationResult<BillingStatementFile>>
{
    private readonly IBillingStatementRepository _statements;
    private readonly IBillingStatementExporter _exporter;
    private readonly BillingSettings _settings;

    public ExportBillingStatementHandler(IBillingStatementRepository statements, IBillingStatementExporter exporter, BillingSettings settings)
    {
        _statements = statements;
        _exporter = exporter;
        _settings = settings;
    }

    public async Task<ApplicationResult<BillingStatementFile>> Handle(ExportBillingStatementQuery request, CancellationToken ct = default)
    {
        var s = await _statements.GetByIdAsync(request.Id, ct);
        if (s is null) return ApplicationResult<BillingStatementFile>.Failure("Statement not found.");
        // The bank's copy never carries the vendor's cost.
        var dto = StatementMapping.Dto(s, _settings, includeCost: false);
        var stem = $"{s.Number}-{s.Month:yyyy-MM}";
        return request.Format.ToLowerInvariant() switch
        {
            "pdf" => ApplicationResult<BillingStatementFile>.Success(new BillingStatementFile($"{stem}.pdf", "application/pdf", _exporter.ToPdf(dto, _settings))),
            "xlsx" or "excel" => ApplicationResult<BillingStatementFile>.Success(new BillingStatementFile($"{stem}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", _exporter.ToExcel(dto, _settings))),
            _ => ApplicationResult<BillingStatementFile>.Failure("Unknown format. Use pdf or xlsx."),
        };
    }
}

// ───────────────────────────── Paid / Void ─────────────────────────────

public sealed record MarkBillingStatementPaidCommand(Guid Id, DateTime PaidOn, string? Reference, string? Note, Guid ActorUserId, string ActorName) : IRequest<ApplicationResult>;

public sealed class MarkBillingStatementPaidHandler : IRequestHandler<MarkBillingStatementPaidCommand, ApplicationResult>
{
    private readonly IBillingStatementRepository _statements;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly AuditService _audit;

    public MarkBillingStatementPaidHandler(IBillingStatementRepository statements, IUserRepository users, IUnitOfWork uow, AuditService audit)
    {
        _statements = statements;
        _users = users;
        _uow = uow;
        _audit = audit;
    }

    public async Task<ApplicationResult> Handle(MarkBillingStatementPaidCommand request, CancellationToken ct = default)
    {
        var access = await BillingAccess.RequireManagerAsync(_users, request.ActorUserId, ct);
        if (!access.IsSuccess) return access;
        var s = await _statements.GetByIdAsync(request.Id, ct);
        if (s is null) return ApplicationResult.Failure("Statement not found.");
        var result = s.MarkPaid(DateTime.SpecifyKind(request.PaidOn, DateTimeKind.Utc), request.ActorUserId, request.Reference, request.Note);
        if (result.IsFailure) return ApplicationResult.Failure(result.Error);
        await _statements.UpdateAsync(s, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.Update, AuditCategory.System,
            $"Billing statement {s.Number} marked paid on {s.PaidAt:dd MMM yyyy}{(s.PaymentReference is null ? "" : $", reference {s.PaymentReference}")}.",
            "BillingStatement", s.Id, s.Number, request.ActorUserId, request.ActorName, ct: ct);
        return ApplicationResult.Success();
    }
}

public sealed record VoidBillingStatementCommand(Guid Id, string Reason, Guid ActorUserId, string ActorName) : IRequest<ApplicationResult<string>>;

public sealed class VoidBillingStatementHandler : IRequestHandler<VoidBillingStatementCommand, ApplicationResult<string>>
{
    private readonly IBillingStatementRepository _statements;
    private readonly IBillingUsageRepository _usage;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly AuditService _audit;

    public VoidBillingStatementHandler(IBillingStatementRepository statements, IBillingUsageRepository usage, IUserRepository users, IUnitOfWork uow, AuditService audit)
    {
        _statements = statements;
        _usage = usage;
        _users = users;
        _uow = uow;
        _audit = audit;
    }

    public async Task<ApplicationResult<string>> Handle(VoidBillingStatementCommand request, CancellationToken ct = default)
    {
        var access = await BillingAccess.RequireManagerAsync(_users, request.ActorUserId, ct);
        if (!access.IsSuccess) return ApplicationResult<string>.Failure(access.Error!);
        var s = await _statements.GetByIdAsync(request.Id, ct);
        if (s is null) return ApplicationResult<string>.Failure("Statement not found.");
        var result = s.Void(request.ActorUserId, request.Reason);
        if (result.IsFailure) return ApplicationResult<string>.Failure(result.Error);
        await _statements.UpdateAsync(s, ct);
        await _uow.SaveChangesAsync(ct);
        var released = await _usage.ReleaseStatementAsync(s.Id, ct);
        await _audit.LogAsync(AuditAction.Update, AuditCategory.System,
            $"Billing statement {s.Number} voided: {s.VoidReason}. {released:N0} rows returned to unbilled.",
            "BillingStatement", s.Id, s.Number, request.ActorUserId, request.ActorName, ct: ct);
        return ApplicationResult<string>.Success($"{s.Number} is voided. Its {released:N0} rows are unbilled again and will appear on the next statement you issue.");
    }
}
