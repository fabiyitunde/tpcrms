using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.LoanApplication.Commands;

public record UpdatePartyInfoCommand(
    Guid ApplicationId,
    Guid PartyId,
    string? BVN,
    decimal? ShareholdingPercent,
    Guid UpdatedByUserId
) : IRequest<ApplicationResult>;

public class UpdatePartyInfoHandler : IRequestHandler<UpdatePartyInfoCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePartyInfoHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(UpdatePartyInfoCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdWithPartiesAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Application not found");

        var result = application.UpdatePartyFields(request.PartyId, request.BVN, request.ShareholdingPercent);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}
