using CRMS.Application.Common;
using CRMS.Application.OfferAcceptance.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.OfferAcceptance.Queries;

public record GetDisbursementChecklistQuery(Guid LoanApplicationId)
    : IRequest<ApplicationResult<DisbursementChecklistDto>>;

public class GetDisbursementChecklistHandler
    : IRequestHandler<GetDisbursementChecklistQuery, ApplicationResult<DisbursementChecklistDto>>
{
    private readonly ILoanApplicationRepository _loanAppRepository;

    public GetDisbursementChecklistHandler(ILoanApplicationRepository loanAppRepository)
    {
        _loanAppRepository = loanAppRepository;
    }

    public async Task<ApplicationResult<DisbursementChecklistDto>> Handle(
        GetDisbursementChecklistQuery request,
        CancellationToken ct = default)
    {
        var loanApp = await _loanAppRepository.GetByIdWithChecklistAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult<DisbursementChecklistDto>.Failure("Loan application not found");

        var items = loanApp.ChecklistItems
            .OrderBy(i => i.SortOrder)
            .Select(i => new ChecklistItemDto(
                Id: i.Id,
                TemplateItemId: i.TemplateItemId,
                ItemName: i.ItemName,
                Description: i.Description,
                IsMandatory: i.IsMandatory,
                ConditionType: i.ConditionType.ToString(),
                SubsequentDueDays: i.SubsequentDueDays,
                RequiresDocumentUpload: i.RequiresDocumentUpload,
                RequiresLegalRatification: i.RequiresLegalRatification,
                CanBeWaived: i.CanBeWaived,
                SortOrder: i.SortOrder,
                Status: i.Status.ToString(),
                IsResolved: i.IsResolved,
                BlocksDisbursement: i.BlocksDisbursement,
                SatisfiedByUserId: i.SatisfiedByUserId,
                SatisfiedAt: i.SatisfiedAt,
                EvidenceDocumentId: i.EvidenceDocumentId,
                LegalRatifiedByUserId: i.LegalRatifiedByUserId,
                LegalRatifiedAt: i.LegalRatifiedAt,
                LegalReturnReason: i.LegalReturnReason,
                WaiverProposedByUserId: i.WaiverProposedByUserId,
                WaiverProposedAt: i.WaiverProposedAt,
                WaiverReason: i.WaiverReason,
                WaiverRatifiedByUserId: i.WaiverRatifiedByUserId,
                WaiverRatifiedAt: i.WaiverRatifiedAt,
                WaiverRejectionReason: i.WaiverRejectionReason,
                DueDate: i.DueDate,
                OriginalDueDate: i.OriginalDueDate,
                ExtensionReason: i.ExtensionReason
            )).ToList();

        var allPrecedentResolved = loanApp.ChecklistItems
            .Where(i => i.ConditionType == Domain.Enums.ConditionType.Precedent && i.IsMandatory)
            .All(i => i.IsResolved);

        return ApplicationResult<DisbursementChecklistDto>.Success(new DisbursementChecklistDto(
            LoanApplicationId: loanApp.Id,
            AllPrecedentResolved: allPrecedentResolved,
            Items: items
        ));
    }
}
