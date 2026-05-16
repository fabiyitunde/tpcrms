using CRMS.Application.Common;
using CRMS.Application.OfferAcceptance.DTOs;
using CRMS.Domain.Constants;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.ProductCatalog.Commands;

// ---------------------------------------------------------------------------
// Add checklist template item to a loan product
// ---------------------------------------------------------------------------

public record AddChecklistTemplateItemCommand(
    Guid LoanProductId,
    string RequestedByUserRole,
    string ItemName,
    string Description,
    bool IsMandatory,
    string ConditionType,
    int? SubsequentDueDays,
    bool RequiresDocumentUpload,
    bool RequiresLegalRatification,
    bool CanBeWaived,
    int SortOrder
) : IRequest<ApplicationResult<ChecklistTemplateItemDto>>;

public class AddChecklistTemplateItemHandler
    : IRequestHandler<AddChecklistTemplateItemCommand, ApplicationResult<ChecklistTemplateItemDto>>
{
    private readonly ILoanProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddChecklistTemplateItemHandler(ILoanProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<ChecklistTemplateItemDto>> Handle(
        AddChecklistTemplateItemCommand request, CancellationToken ct = default)
    {
        if (request.RequestedByUserRole is not (Roles.SystemAdmin or Roles.RiskManager))
            return ApplicationResult<ChecklistTemplateItemDto>.Failure("Only SystemAdmin or RiskManager may configure checklist templates");

        if (!Enum.TryParse<ConditionType>(request.ConditionType, ignoreCase: true, out var conditionType))
            return ApplicationResult<ChecklistTemplateItemDto>.Failure($"Invalid condition type: {request.ConditionType}");

        var product = await _productRepository.GetByIdAsync(request.LoanProductId, ct);
        if (product == null)
            return ApplicationResult<ChecklistTemplateItemDto>.Failure("Loan product not found");

        var result = product.AddChecklistTemplateItem(
            request.ItemName,
            request.Description,
            request.IsMandatory,
            conditionType,
            request.SubsequentDueDays,
            request.RequiresDocumentUpload,
            request.RequiresLegalRatification,
            request.CanBeWaived,
            request.SortOrder);

        if (result.IsFailure)
            return ApplicationResult<ChecklistTemplateItemDto>.Failure(result.Error);

        await _productRepository.AddChecklistTemplateItemAsync(result.Value, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var item = result.Value;
        return ApplicationResult<ChecklistTemplateItemDto>.Success(new ChecklistTemplateItemDto(
            item.Id,
            item.LoanProductId,
            item.ItemName,
            item.Description,
            item.IsMandatory,
            item.ConditionType.ToString(),
            item.SubsequentDueDays,
            item.RequiresDocumentUpload,
            item.RequiresLegalRatification,
            item.CanBeWaived,
            item.SortOrder,
            item.IsActive
        ));
    }
}

// ---------------------------------------------------------------------------
// Update checklist template item
// ---------------------------------------------------------------------------

public record UpdateChecklistTemplateItemCommand(
    Guid LoanProductId,
    Guid ItemId,
    string RequestedByUserRole,
    string ItemName,
    string Description,
    bool IsMandatory,
    string ConditionType,
    int? SubsequentDueDays,
    bool RequiresDocumentUpload,
    bool RequiresLegalRatification,
    bool CanBeWaived,
    int SortOrder
) : IRequest<ApplicationResult>;

public class UpdateChecklistTemplateItemHandler : IRequestHandler<UpdateChecklistTemplateItemCommand, ApplicationResult>
{
    private readonly ILoanProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateChecklistTemplateItemHandler(ILoanProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(
        UpdateChecklistTemplateItemCommand request, CancellationToken ct = default)
    {
        if (request.RequestedByUserRole is not (Roles.SystemAdmin or Roles.RiskManager))
            return ApplicationResult.Failure("Only SystemAdmin or RiskManager may configure checklist templates");

        if (!Enum.TryParse<ConditionType>(request.ConditionType, ignoreCase: true, out var conditionType))
            return ApplicationResult.Failure($"Invalid condition type: {request.ConditionType}");

        var product = await _productRepository.GetByIdAsync(request.LoanProductId, ct);
        if (product == null)
            return ApplicationResult.Failure("Loan product not found");

        var result = product.UpdateChecklistTemplateItem(
            request.ItemId,
            request.ItemName,
            request.Description,
            request.IsMandatory,
            conditionType,
            request.SubsequentDueDays,
            request.RequiresDocumentUpload,
            request.RequiresLegalRatification,
            request.CanBeWaived,
            request.SortOrder);

        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _productRepository.UpdateAsync(product, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

// ---------------------------------------------------------------------------
// Remove (soft-delete via deactivate) a checklist template item
// ---------------------------------------------------------------------------

public record RemoveChecklistTemplateItemCommand(
    Guid LoanProductId,
    Guid ItemId,
    string RequestedByUserRole
) : IRequest<ApplicationResult>;

public class RemoveChecklistTemplateItemHandler : IRequestHandler<RemoveChecklistTemplateItemCommand, ApplicationResult>
{
    private readonly ILoanProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveChecklistTemplateItemHandler(ILoanProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(
        RemoveChecklistTemplateItemCommand request, CancellationToken ct = default)
    {
        if (request.RequestedByUserRole is not (Roles.SystemAdmin or Roles.RiskManager))
            return ApplicationResult.Failure("Only SystemAdmin or RiskManager may configure checklist templates");

        var product = await _productRepository.GetByIdAsync(request.LoanProductId, ct);
        if (product == null)
            return ApplicationResult.Failure("Loan product not found");

        var result = product.RemoveChecklistTemplateItem(request.ItemId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _productRepository.UpdateAsync(product, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
