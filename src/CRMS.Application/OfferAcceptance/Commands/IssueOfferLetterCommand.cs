using CRMS.Application.Common;
using CRMS.Domain.Constants;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.OfferAcceptance.Commands;

/// <summary>
/// Transitions the loan application from Approved → OfferGenerated and seeds
/// the disbursement checklist from the loan product's template.
/// Called by the branch LoanOfficer when the offer letter is physically issued to the customer.
/// </summary>
public record IssueOfferLetterCommand(
    Guid LoanApplicationId,
    Guid IssuedByUserId
) : IRequest<ApplicationResult>;

public class IssueOfferLetterHandler : IRequestHandler<IssueOfferLetterCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly ILoanProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public IssueOfferLetterHandler(
        ILoanApplicationRepository loanAppRepository,
        ILoanProductRepository productRepository,
        IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(IssueOfferLetterCommand request, CancellationToken ct = default)
    {
        var loanApp = await _loanAppRepository.GetByIdWithChecklistAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult.Failure("Loan application not found");

        var issueResult = loanApp.IssueOfferLetter(request.IssuedByUserId);
        if (!issueResult.IsSuccess)
            return ApplicationResult.Failure(issueResult.Error!);

        // Load the product to get the disbursement checklist template
        var product = await _productRepository.GetByIdAsync(loanApp.LoanProductId, ct);
        if (product != null && product.DisbursementChecklist.Any())
        {
            loanApp.SeedChecklistItems(product.DisbursementChecklist);
        }

        _loanAppRepository.Update(loanApp);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}
