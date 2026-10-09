using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>Live read-through from Fineract — no local persistence for balances/schedule, same
/// pattern as NAMP's GetNampLoanAccountQuery. Reuses the already-shared, already-generic
/// IFineractDirectService.GetLoanDetailAsync — zero new Fineract integration.</summary>
public record GetRhshfLoanAccountQuery(string Reference) : IRequest<ApplicationResult<RhshfLoanAccountDto>>;

public class GetRhshfLoanAccountHandler : IRequestHandler<GetRhshfLoanAccountQuery, ApplicationResult<RhshfLoanAccountDto>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IFineractDirectService _fineract;

    public GetRhshfLoanAccountHandler(IRhshfCreditProfileRepository repo, IFineractDirectService fineract)
    {
        _repo = repo;
        _fineract = fineract;
    }

    public async Task<ApplicationResult<RhshfLoanAccountDto>> Handle(GetRhshfLoanAccountQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<RhshfLoanAccountDto>.Failure("Case not found.");

        var booked = profile.Disbursements
            .Where(d => d.Status == RhshfDisbursementStatus.Booked && d.FineractLoanId.HasValue)
            .OrderByDescending(d => d.BookedAt)
            .FirstOrDefault();
        if (booked is null)
            return ApplicationResult<RhshfLoanAccountDto>.Failure("No Core Banking loan account is linked to this case.");

        var result = await _fineract.GetLoanDetailAsync(booked.FineractLoanId!.Value, ct);
        if (!result.IsSuccess)
        {
            var reason = System.Text.RegularExpressions.Regex.Replace(
                result.Error ?? "unknown error", "fineract", "Core Banking",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return ApplicationResult<RhshfLoanAccountDto>.Failure($"Could not load loan account from Core Banking: {reason}");
        }

        var loan = result.Value;
        var now = DateTime.UtcNow;

        var dto = new RhshfLoanAccountDto(
            LoanId: loan.Id,
            AccountNo: loan.AccountNo,
            ProductName: loan.ProductName,
            Status: loan.Status,
            DisbursementDate: loan.DisbursementDate,
            MaturityDate: loan.MaturityDate,
            TotalExpectedRepayment: loan.Summary.TotalExpectedRepayment,
            TotalRepayment: loan.Summary.TotalRepayment,
            TotalOutstanding: loan.Summary.TotalOutstanding,
            PrincipalDisbursed: loan.Summary.PrincipalDisbursed,
            PrincipalPaid: loan.Summary.PrincipalPaid,
            PrincipalOutstanding: loan.Summary.PrincipalOutstanding,
            InterestCharged: loan.Summary.InterestCharged,
            InterestPaid: loan.Summary.InterestPaid,
            InterestOutstanding: loan.Summary.InterestOutstanding,
            PenaltyChargesOutstanding: loan.Summary.PenaltyChargesOutstanding,
            Schedule: loan.RepaymentSchedule
                .Where(p => p.Period > 0)
                .Select(p => new RhshfLoanSchedulePeriodDto(
                    Period: p.Period,
                    DueDate: p.DueDate,
                    PrincipalDue: p.PrincipalDue,
                    PrincipalPaid: p.PrincipalPaid,
                    InterestDue: p.InterestDue,
                    InterestPaid: p.InterestPaid,
                    TotalDue: p.TotalDue,
                    TotalPaid: p.TotalPaid,
                    TotalOutstanding: p.TotalOutstanding,
                    Complete: p.Complete,
                    IsOverdue: !p.Complete && p.DueDate < now
                ))
                .ToList()
        );

        return ApplicationResult<RhshfLoanAccountDto>.Success(dto);
    }
}
