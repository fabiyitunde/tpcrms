using CRMS.Application.Common;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Application.Rhshf.Queries;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>
/// Assembles the RH-SHF Loan Pack by reusing the Detail page's own section query handlers (so the pack
/// and the on-screen tabs can never drift), then hands the composite to the PDF generator. Each section
/// is optional — a case at Risk Review has no committee/offer/disbursement yet — so missing sections are
/// simply omitted rather than failing the pack.
/// </summary>
public record GenerateRhshfLoanPackCommand(
    string Reference,
    Guid GeneratedByUserId,
    string GeneratedByUserName,
    string BankName) : IRequest<ApplicationResult<byte[]>>;

public class GenerateRhshfLoanPackHandler : IRequestHandler<GenerateRhshfLoanPackCommand, ApplicationResult<byte[]>>
{
    private readonly GetRhshfCaseWorkspaceHandler _workspace;
    private readonly GetRhshfDirectorsHandler _directors;
    private readonly GetRhshfBureauReportsHandler _bureau;
    private readonly GetRhshfFinancialAppraisalHandler _appraisal;
    private readonly GetRhshfCollateralHandler _collateral;
    private readonly GetRhshfGuarantorsHandler _guarantors;
    private readonly GetRhshfCommitteeReviewHandler _committee;
    private readonly GetRhshfOfferHandler _offer;
    private readonly GetRhshfDisbursementHistoryHandler _disbursements;
    private readonly GetRhshfAdvisoryHandler _advisory;
    private readonly GetRhshfStatusHistoryHandler _history;
    private readonly IRhshfLoanPackGenerator _generator;

    public GenerateRhshfLoanPackHandler(
        GetRhshfCaseWorkspaceHandler workspace,
        GetRhshfDirectorsHandler directors,
        GetRhshfBureauReportsHandler bureau,
        GetRhshfFinancialAppraisalHandler appraisal,
        GetRhshfCollateralHandler collateral,
        GetRhshfGuarantorsHandler guarantors,
        GetRhshfCommitteeReviewHandler committee,
        GetRhshfOfferHandler offer,
        GetRhshfDisbursementHistoryHandler disbursements,
        GetRhshfAdvisoryHandler advisory,
        GetRhshfStatusHistoryHandler history,
        IRhshfLoanPackGenerator generator)
    {
        _workspace = workspace;
        _directors = directors;
        _bureau = bureau;
        _appraisal = appraisal;
        _collateral = collateral;
        _guarantors = guarantors;
        _committee = committee;
        _offer = offer;
        _disbursements = disbursements;
        _advisory = advisory;
        _history = history;
        _generator = generator;
    }

    public async Task<ApplicationResult<byte[]>> Handle(GenerateRhshfLoanPackCommand request, CancellationToken ct = default)
    {
        // The workspace is the one required section — everything else is a best-effort add-on. Calls are
        // sequential on purpose: they share one scoped DbContext, so running them concurrently would break.
        var workspace = await _workspace.Handle(new GetRhshfCaseWorkspaceQuery(request.Reference), ct);
        if (!workspace.IsSuccess || workspace.Data is null)
            return ApplicationResult<byte[]>.Failure(workspace.Error ?? "Case not found.");

        var directors = (await _directors.Handle(new GetRhshfDirectorsQuery(request.Reference), ct)).Data;
        var bureau = (await _bureau.Handle(new GetRhshfBureauReportsQuery(request.Reference), ct)).Data ?? [];
        var appraisal = (await _appraisal.Handle(new GetRhshfFinancialAppraisalQuery(request.Reference), ct)).Data;
        var collateral = (await _collateral.Handle(new GetRhshfCollateralQuery(request.Reference), ct)).Data ?? [];
        var guarantors = (await _guarantors.Handle(new GetRhshfGuarantorsQuery(request.Reference), ct)).Data ?? [];
        var committee = (await _committee.Handle(new GetRhshfCommitteeReviewQuery(request.Reference), ct)).Data;
        var offer = (await _offer.Handle(new GetRhshfOfferQuery(request.Reference), ct)).Data;
        var disbursements = (await _disbursements.Handle(new GetRhshfDisbursementHistoryQuery(request.Reference), ct)).Data ?? [];
        var advisory = (await _advisory.Handle(new GetRhshfAdvisoryQuery(request.Reference), ct)).Data;
        var history = (await _history.Handle(new GetRhshfStatusHistoryQuery(request.Reference), ct)).Data ?? [];

        var data = new RhshfLoanPackData(
            BankName: request.BankName,
            GeneratedBy: request.GeneratedByUserName,
            GeneratedAt: DateTime.UtcNow,
            Workspace: workspace.Data,
            Directors: directors,
            BureauReports: bureau,
            FinancialAppraisal: appraisal,
            Collaterals: collateral,
            Guarantors: guarantors,
            CommitteeReview: committee,
            Offer: offer,
            Disbursements: disbursements,
            Advisory: advisory,
            StatusHistory: history);

        var bytes = await _generator.GenerateAsync(data, ct);
        return ApplicationResult<byte[]>.Success(bytes);
    }
}
