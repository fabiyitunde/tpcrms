using CRMS.Application.Common;
using CRMS.Application.Rhshf.Interfaces;
using CRMS.Application.Rhshf.Services;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>
/// What the bank pays out of an RH-SHF disbursement, shown on the Book Disbursement form.
/// <para>
/// The CRMS books the loan only. Core banking credits the product's settlement account, and the
/// bank pays each supplier from there itself — so the form shows the approved plan the bank works
/// from rather than asking for one supplier account it would never use. Built by the same routine as
/// the offer letter's Schedule B, so the two cannot disagree.
/// </para>
/// </summary>
public record GetRhshfDisbursementPlanQuery(string Reference) : IRequest<ApplicationResult<List<RhshfOfferPlanRow>>>;

public class GetRhshfDisbursementPlanHandler : IRequestHandler<GetRhshfDisbursementPlanQuery, ApplicationResult<List<RhshfOfferPlanRow>>>
{
    private readonly IRhshfCreditProfileRepository _repo;

    public GetRhshfDisbursementPlanHandler(IRhshfCreditProfileRepository repo) => _repo = repo;

    public async Task<ApplicationResult<List<RhshfOfferPlanRow>>> Handle(GetRhshfDisbursementPlanQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfOfferPlanRow>>.Failure("Case not found.");

        var amount = profile.ApprovedAmount ?? profile.TotalEopValue;
        return ApplicationResult<List<RhshfOfferPlanRow>>.Success(RhshfOfferDocumentDataBuilder.BuildPlanRows(profile, amount));
    }
}
