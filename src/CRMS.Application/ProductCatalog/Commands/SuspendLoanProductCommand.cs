using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.ProductCatalog.Commands;

public record SuspendLoanProductCommand(Guid Id) : IRequest<ApplicationResult>;

public class SuspendLoanProductHandler : IRequestHandler<SuspendLoanProductCommand, ApplicationResult>
{
    private readonly ILoanProductRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SuspendLoanProductHandler(ILoanProductRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(SuspendLoanProductCommand request, CancellationToken ct = default)
    {
        var product = await _repository.GetByIdAsync(request.Id, ct);
        if (product == null)
            return ApplicationResult.Failure("Product not found");

        var result = product.Suspend();
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _repository.UpdateAsync(product, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}
