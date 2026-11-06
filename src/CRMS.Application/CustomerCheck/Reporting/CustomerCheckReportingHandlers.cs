using System.Globalization;
using System.Text;
using CRMS.Application.Common;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CustomerCheck.Reporting;

// ── Overview ─────────────────────────────────────────────────────────────────────────────────────

public record GetCustomerCheckOverviewQuery(DateTime FromUtc, DateTime ToUtc, Guid? ConsumerId) : IRequest<ApplicationResult<CustomerCheckOverviewDto>>;

public class GetCustomerCheckOverviewHandler : IRequestHandler<GetCustomerCheckOverviewQuery, ApplicationResult<CustomerCheckOverviewDto>>
{
    private readonly ICustomerCheckReporting _reporting;

    public GetCustomerCheckOverviewHandler(ICustomerCheckReporting reporting) => _reporting = reporting;

    public async Task<ApplicationResult<CustomerCheckOverviewDto>> Handle(GetCustomerCheckOverviewQuery request, CancellationToken ct = default)
    {
        if (request.FromUtc >= request.ToUtc)
            return ApplicationResult<CustomerCheckOverviewDto>.Failure("The period's start must be before its end.");
        if ((request.ToUtc - request.FromUtc).TotalDays > 400)
            return ApplicationResult<CustomerCheckOverviewDto>.Failure("The period may be at most 400 days.");
        return ApplicationResult<CustomerCheckOverviewDto>.Success(await _reporting.GetOverviewAsync(request.FromUtc, request.ToUtc, request.ConsumerId, ct));
    }
}

// ── Statement export ─────────────────────────────────────────────────────────────────────────────

public record ExportCustomerCheckStatementQuery(Guid ConsumerId, DateTime FromUtc, DateTime ToUtc) : IRequest<ApplicationResult<CustomerCheckStatementFile>>;

public record CustomerCheckStatementFile(string FileName, byte[] Content, CustomerCheckStatementDto Statement);

public class ExportCustomerCheckStatementHandler : IRequestHandler<ExportCustomerCheckStatementQuery, ApplicationResult<CustomerCheckStatementFile>>
{
    private readonly ICustomerCheckReporting _reporting;

    public ExportCustomerCheckStatementHandler(ICustomerCheckReporting reporting) => _reporting = reporting;

    public async Task<ApplicationResult<CustomerCheckStatementFile>> Handle(ExportCustomerCheckStatementQuery request, CancellationToken ct = default)
    {
        if (request.FromUtc >= request.ToUtc)
            return ApplicationResult<CustomerCheckStatementFile>.Failure("The period's start must be before its end.");
        var statement = await _reporting.GetStatementAsync(request.ConsumerId, request.FromUtc, request.ToUtc, ct);
        var safeName = string.Concat(statement.ConsumerName.Where(c => char.IsLetterOrDigit(c) || c == ' ')).Trim().Replace(' ', '-');
        var fileName = $"customer-credit-check-statement-{safeName}-{request.FromUtc:yyyyMMdd}-{request.ToUtc.AddDays(-1):yyyyMMdd}.csv";
        return ApplicationResult<CustomerCheckStatementFile>.Success(new CustomerCheckStatementFile(fileName, Csv(statement), statement));
    }

    /// <summary>A statement a finance team can open in a spreadsheet: one line per check, then
    /// the totals by billable item. Identifiers appear only as their last four digits.</summary>
    public static byte[] Csv(CustomerCheckStatementDto s)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.AppendLine($"Customer credit check statement,{Q(s.ConsumerName)}");
        sb.AppendLine($"Period,{s.From:yyyy-MM-dd} to {s.To.AddDays(-1):yyyy-MM-dd}");
        sb.AppendLine($"Currency,{s.Currency}");
        sb.AppendLine();
        sb.AppendLine("Queued (UTC),Job id,Request id,Status,Sections,Outcomes,Identity calls,Bureau calls,Score calls,Fraud calls,Advisory calls,Amount");
        foreach (var l in s.Lines)
            sb.AppendLine(string.Join(",",
                l.QueuedAt.ToString("yyyy-MM-dd HH:mm:ss", inv), l.JobId, Q(l.RequestId), l.Status, Q(l.Sections), Q(l.SectionOutcomes ?? ""),
                l.Calls.Identity, l.Calls.Bureau, l.Calls.Score, l.Calls.Fraud, l.Calls.Advisory, l.Amount.ToString("0.00", inv)));
        sb.AppendLine();
        sb.AppendLine("Item,Quantity,Unit price,Amount");
        foreach (var t in s.ByItem)
            sb.AppendLine($"{Q(t.DisplayName)},{t.Quantity},{t.UnitPrice.ToString("0.00", inv)},{t.Amount.ToString("0.00", inv)}");
        sb.AppendLine($"Total,,,{s.Total.ToString("0.00", inv)}");
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();

        static string Q(string v) => v.Contains(',') || v.Contains('"') || v.Contains('\n') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}

// ── Jobs (staff view) ────────────────────────────────────────────────────────────────────────────

public record SearchCustomerCheckJobsQuery(Guid? ConsumerId, DateTime? FromUtc, DateTime? ToUtc, string? Status, string? RequestId, int Page, int PageSize)
    : IRequest<ApplicationResult<CustomerCheckJobPageDto>>;

public record CustomerCheckJobPageDto(int Page, int PageSize, int Total, IReadOnlyList<CustomerCheckJobSummaryDto> Items);

public class SearchCustomerCheckJobsHandler : IRequestHandler<SearchCustomerCheckJobsQuery, ApplicationResult<CustomerCheckJobPageDto>>
{
    private readonly ICustomerCheckReporting _reporting;

    public SearchCustomerCheckJobsHandler(ICustomerCheckReporting reporting) => _reporting = reporting;

    public async Task<ApplicationResult<CustomerCheckJobPageDto>> Handle(SearchCustomerCheckJobsQuery request, CancellationToken ct = default)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 200);
        var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim().ToUpperInvariant();
        var filter = new CustomerCheckJobFilter(request.ConsumerId, request.FromUtc, request.ToUtc, status,
            string.IsNullOrWhiteSpace(request.RequestId) ? null : request.RequestId.Trim(), null, page, size);
        var (items, total) = await _reporting.SearchJobsAsync(filter, ct);
        return ApplicationResult<CustomerCheckJobPageDto>.Success(new CustomerCheckJobPageDto(page, size, total, items));
    }
}

public record GetCustomerCheckJobDetailQuery(Guid JobId) : IRequest<ApplicationResult<CustomerCheckJobDetailDto>>;

public class GetCustomerCheckJobDetailHandler : IRequestHandler<GetCustomerCheckJobDetailQuery, ApplicationResult<CustomerCheckJobDetailDto>>
{
    private readonly ICustomerCheckReporting _reporting;

    public GetCustomerCheckJobDetailHandler(ICustomerCheckReporting reporting) => _reporting = reporting;

    public async Task<ApplicationResult<CustomerCheckJobDetailDto>> Handle(GetCustomerCheckJobDetailQuery request, CancellationToken ct = default)
    {
        var detail = await _reporting.GetJobDetailAsync(request.JobId, ct);
        return detail is null
            ? ApplicationResult<CustomerCheckJobDetailDto>.Failure("Check not found.")
            : ApplicationResult<CustomerCheckJobDetailDto>.Success(detail);
    }
}

/// <summary>Staff re-send of a callback the automatic attempts gave up on, or that the partner
/// says never arrived. The API host's worker picks it up on its next pass.</summary>
public record ResendCustomerCheckCallbackCommand(Guid JobId, Guid RequestedByUserId) : IRequest<ApplicationResult>;

public class ResendCustomerCheckCallbackHandler : IRequestHandler<ResendCustomerCheckCallbackCommand, ApplicationResult>
{
    private readonly ICustomerCheckJobRepository _jobs;
    private readonly ICustomerCheckJobSignal _signal;
    private readonly IUnitOfWork _unitOfWork;

    public ResendCustomerCheckCallbackHandler(ICustomerCheckJobRepository jobs, ICustomerCheckJobSignal signal, IUnitOfWork unitOfWork)
    {
        _jobs = jobs;
        _signal = signal;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ResendCustomerCheckCallbackCommand request, CancellationToken ct = default)
    {
        var job = await _jobs.GetByIdAsync(request.JobId, ct);
        if (job is null)
            return ApplicationResult.Failure("Check not found.");
        var result = job.ResendCallback(DateTime.UtcNow);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);
        job.SetAuditInfo(request.RequestedByUserId.ToString());
        _jobs.Update(job);
        await _unitOfWork.SaveChangesAsync(ct);
        _signal.Nudge();
        return ApplicationResult.Success();
    }
}

// ── Prices ───────────────────────────────────────────────────────────────────────────────────────

public record GetCustomerCheckPricesQuery : IRequest<ApplicationResult<List<CustomerCheckPriceDto>>>;

public class GetCustomerCheckPricesHandler : IRequestHandler<GetCustomerCheckPricesQuery, ApplicationResult<List<CustomerCheckPriceDto>>>
{
    private readonly ICustomerCheckPriceRepository _prices;

    public GetCustomerCheckPricesHandler(ICustomerCheckPriceRepository prices) => _prices = prices;

    public async Task<ApplicationResult<List<CustomerCheckPriceDto>>> Handle(GetCustomerCheckPricesQuery request, CancellationToken ct = default)
    {
        var stored = (await _prices.GetAllAsync(ct)).ToDictionary(p => p.Item, StringComparer.OrdinalIgnoreCase);
        // Every item is listed, priced or not, so the table never has a hole in it.
        var list = CustomerCheckPriceItems.All.Select(i => stored.TryGetValue(i.Item, out var p)
            ? new CustomerCheckPriceDto(p.Item, p.DisplayName, p.UnitPrice, p.Currency, p.ModifiedAt ?? p.CreatedAt, p.ModifiedBy ?? p.CreatedBy)
            : new CustomerCheckPriceDto(i.Item, i.DisplayName, 0m, "NGN", null, null)).ToList();
        return ApplicationResult<List<CustomerCheckPriceDto>>.Success(list);
    }
}

public record SetCustomerCheckPriceCommand(string Item, decimal UnitPrice, Guid ModifiedByUserId) : IRequest<ApplicationResult>;

public class SetCustomerCheckPriceHandler : IRequestHandler<SetCustomerCheckPriceCommand, ApplicationResult>
{
    private readonly ICustomerCheckPriceRepository _prices;
    private readonly IUnitOfWork _unitOfWork;

    public SetCustomerCheckPriceHandler(ICustomerCheckPriceRepository prices, IUnitOfWork unitOfWork)
    {
        _prices = prices;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(SetCustomerCheckPriceCommand request, CancellationToken ct = default)
    {
        var known = CustomerCheckPriceItems.All.FirstOrDefault(i => string.Equals(i.Item, request.Item?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (known == default)
            return ApplicationResult.Failure("Unknown billable item.");

        var existing = (await _prices.GetAllAsync(ct)).FirstOrDefault(p => string.Equals(p.Item, known.Item, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            var created = CustomerCheckPrice.Create(known.Item, known.DisplayName, request.UnitPrice, "NGN", request.ModifiedByUserId);
            if (created.IsFailure) return ApplicationResult.Failure(created.Error);
            await _prices.AddAsync(created.Value, ct);
        }
        else
        {
            var set = existing.SetPrice(request.UnitPrice, request.ModifiedByUserId);
            if (set.IsFailure) return ApplicationResult.Failure(set.Error);
            _prices.Update(existing);
        }
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
