using CRMS.Application.Common;
using CRMS.Application.OfferAcceptance.DTOs;
using CRMS.Application.OfferAcceptance.Interfaces;
using CRMS.Domain.Constants;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.OfferAcceptance.Commands;

/// <summary>
/// Operations confirms that all Conditions Precedent have been satisfied/waived
/// and that the customer has formally accepted the offer letter.
/// Transitions the loan application from OfferGenerated → OfferAccepted,
/// then generates and stores the Disbursement Memo PDF for audit/CBN compliance.
/// </summary>
public record ConfirmOfferAcceptanceCommand(
    Guid LoanApplicationId,
    Guid ConfirmedByUserId,
    string ConfirmedByUserRole,
    string ConfirmedByUserName,
    string BankName,
    DateTime CustomerSignedAt,
    OfferAcceptanceMethod AcceptanceMethod,
    bool KfsAcknowledged
) : IRequest<ApplicationResult>;

public class ConfirmOfferAcceptanceHandler : IRequestHandler<ConfirmOfferAcceptanceCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly IDisbursementMemoPdfGenerator _pdfGenerator;
    private readonly IFileStorageService _fileStorageService;
    private readonly IUnitOfWork _unitOfWork;

    public ConfirmOfferAcceptanceHandler(
        ILoanApplicationRepository loanAppRepository,
        IDisbursementMemoPdfGenerator pdfGenerator,
        IFileStorageService fileStorageService,
        IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _pdfGenerator = pdfGenerator;
        _fileStorageService = fileStorageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ConfirmOfferAcceptanceCommand request, CancellationToken ct = default)
    {
        if (request.ConfirmedByUserRole is not Roles.Operations)
            return ApplicationResult.Failure("Only Operations may confirm offer acceptance");

        var loanApp = await _loanAppRepository.GetByIdWithChecklistAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult.Failure("Loan application not found");

        // Domain method validates status, KFS acknowledgement and CP gate
        var acceptResult = loanApp.AcceptOffer(
            request.ConfirmedByUserId,
            request.CustomerSignedAt,
            request.AcceptanceMethod,
            request.KfsAcknowledged);
        if (!acceptResult.IsSuccess)
            return ApplicationResult.Failure(acceptResult.Error!);

        // Generate Disbursement Memo PDF
        var memoRequest = new DisbursementMemoRequest(
            ApplicationNumber: loanApp.ApplicationNumber,
            CustomerName: loanApp.CustomerName,
            ApprovedAmount: loanApp.ApprovedAmount?.Amount ?? loanApp.RequestedAmount.Amount,
            ApprovedTenorMonths: loanApp.ApprovedTenorMonths ?? loanApp.RequestedTenorMonths,
            ApprovedInterestRate: loanApp.ApprovedInterestRate ?? loanApp.InterestRatePerAnnum,
            OfferIssuedAt: loanApp.OfferIssuedAt!.Value,
            OfferAcceptedAt: loanApp.OfferAcceptedAt!.Value,
            AcceptedByUserName: request.ConfirmedByUserName,
            BankName: request.BankName,
            ChecklistItems: loanApp.ChecklistItems.OrderBy(i => i.SortOrder).Select(i => new DisbursementChecklistItemDto(
                ItemName: i.ItemName,
                ConditionType: i.ConditionType.ToString(),
                IsMandatory: i.IsMandatory,
                Status: i.Status.ToString(),
                SatisfiedByUserName: null, // Resolved later via user lookup if needed
                SatisfiedAt: i.SatisfiedAt,
                WaiverProposedByUserName: null,
                WaiverReason: i.WaiverReason,
                WaiverApprovedByUserName: null,
                WaiverRatifiedAt: i.WaiverRatifiedAt,
                DueDate: i.DueDate
            )).ToList()
        );

        var pdfBytes = await _pdfGenerator.GenerateAsync(memoRequest, ct);

        var fileName = $"DisbursementMemo_{loanApp.ApplicationNumber}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        await _fileStorageService.UploadAsync(
            containerName: "disbursementmemos",
            fileName: $"{loanApp.ApplicationNumber}/{fileName}",
            content: pdfBytes,
            contentType: "application/pdf",
            ct: ct);

        _loanAppRepository.Update(loanApp);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}
