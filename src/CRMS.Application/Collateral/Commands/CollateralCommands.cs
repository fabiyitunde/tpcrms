using CRMS.Application.Collateral.DTOs;
using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Collateral;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.ValueObjects;

namespace CRMS.Application.Collateral.Commands;

public record AddCollateralCommand(
    Guid LoanApplicationId,
    CollateralType Type,
    string Description,
    Guid CreatedByUserId,
    string? AssetIdentifier = null,
    string? Location = null,
    string? OwnerName = null,
    string? OwnershipType = null
) : IRequest<ApplicationResult<CollateralDto>>;

public class AddCollateralHandler : IRequestHandler<AddCollateralCommand, ApplicationResult<CollateralDto>>
{
    private readonly ICollateralRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddCollateralHandler(ICollateralRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CollateralDto>> Handle(AddCollateralCommand request, CancellationToken ct = default)
    {
        var result = Domain.Aggregates.Collateral.Collateral.Create(
            request.LoanApplicationId,
            request.Type,
            request.Description,
            request.CreatedByUserId,
            request.AssetIdentifier,
            request.Location,
            request.OwnerName,
            request.OwnershipType
        );

        if (result.IsFailure)
            return ApplicationResult<CollateralDto>.Failure(result.Error);

        await _repository.AddAsync(result.Value, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CollateralDto>.Success(MapToDto(result.Value));
    }

    private static CollateralDto MapToDto(Domain.Aggregates.Collateral.Collateral c) => new(
        c.Id, c.LoanApplicationId, c.CollateralReference, c.Type.ToString(), c.Status.ToString(),
        c.PerfectionStatus.ToString(), c.Description, c.AssetIdentifier, c.Location, c.OwnerName,
        c.OwnershipType, c.MarketValue?.Amount, c.ForcedSaleValue?.Amount, c.HaircutPercentage,
        c.AcceptableValue?.Amount, c.MarketValue?.Currency, c.LastValuationDate, c.NextRevaluationDue,
        c.LienType?.ToString(), c.LienReference, c.LienRegistrationDate, c.IsInsured,
        c.InsurancePolicyNumber, c.InsuredValue?.Amount, c.InsuranceExpiryDate, c.CreatedAt, c.ApprovedAt,
        c.RejectionReason, [], []
    );
}

public record SetCollateralValuationCommand(
    Guid CollateralId,
    decimal MarketValue,
    decimal? ForcedSaleValue,
    string Currency,
    decimal? HaircutPercentage
) : IRequest<ApplicationResult<CollateralDto>>;

public class SetCollateralValuationHandler : IRequestHandler<SetCollateralValuationCommand, ApplicationResult<CollateralDto>>
{
    private readonly ICollateralRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public SetCollateralValuationHandler(ICollateralRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CollateralDto>> Handle(SetCollateralValuationCommand request, CancellationToken ct = default)
    {
        var collateral = await _repository.GetByIdWithDetailsAsync(request.CollateralId, ct);
        if (collateral == null)
            return ApplicationResult<CollateralDto>.Failure("Collateral not found");

        var marketValue = Money.Create(request.MarketValue, request.Currency);
        var forcedSaleValue = request.ForcedSaleValue.HasValue 
            ? Money.Create(request.ForcedSaleValue.Value, request.Currency) 
            : null;

        var result = collateral.SetValuation(marketValue, forcedSaleValue, request.HaircutPercentage);
        if (result.IsFailure)
            return ApplicationResult<CollateralDto>.Failure(result.Error);

        _repository.Update(collateral);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CollateralDto>.Success(MapToDto(collateral));
    }

    private static CollateralDto MapToDto(Domain.Aggregates.Collateral.Collateral c) => new(
        c.Id, c.LoanApplicationId, c.CollateralReference, c.Type.ToString(), c.Status.ToString(),
        c.PerfectionStatus.ToString(), c.Description, c.AssetIdentifier, c.Location, c.OwnerName,
        c.OwnershipType, c.MarketValue?.Amount, c.ForcedSaleValue?.Amount, c.HaircutPercentage,
        c.AcceptableValue?.Amount, c.MarketValue?.Currency, c.LastValuationDate, c.NextRevaluationDue,
        c.LienType?.ToString(), c.LienReference, c.LienRegistrationDate, c.IsInsured,
        c.InsurancePolicyNumber, c.InsuredValue?.Amount, c.InsuranceExpiryDate, c.CreatedAt, c.ApprovedAt,
        c.RejectionReason,
        c.Valuations.Select(v => new CollateralValuationDto(v.Id, v.Type.ToString(), v.Status.ToString(),
            v.ValuationDate, v.MarketValue.Amount, v.ForcedSaleValue?.Amount, v.MarketValue.Currency,
            v.ValuerName, v.ValuerCompany, v.ExpiryDate)).ToList(),
        c.Documents.Select(d => new CollateralDocumentDto(d.Id, d.DocumentType, d.FileName,
            d.FileSizeBytes, d.IsVerified, d.UploadedAt)).ToList()
    );
}

public record ApproveCollateralCommand(Guid CollateralId, Guid ApprovedByUserId) : IRequest<ApplicationResult<CollateralDto>>;

public class ApproveCollateralHandler : IRequestHandler<ApproveCollateralCommand, ApplicationResult<CollateralDto>>
{
    private readonly ICollateralRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveCollateralHandler(ICollateralRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CollateralDto>> Handle(ApproveCollateralCommand request, CancellationToken ct = default)
    {
        var collateral = await _repository.GetByIdWithDetailsAsync(request.CollateralId, ct);
        if (collateral == null)
            return ApplicationResult<CollateralDto>.Failure("Collateral not found");

        var result = collateral.Approve(request.ApprovedByUserId);
        if (result.IsFailure)
            return ApplicationResult<CollateralDto>.Failure(result.Error);

        _repository.Update(collateral);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CollateralDto>.Success(MapToDto(collateral));
    }

    private static CollateralDto MapToDto(Domain.Aggregates.Collateral.Collateral c) => new(
        c.Id, c.LoanApplicationId, c.CollateralReference, c.Type.ToString(), c.Status.ToString(),
        c.PerfectionStatus.ToString(), c.Description, c.AssetIdentifier, c.Location, c.OwnerName,
        c.OwnershipType, c.MarketValue?.Amount, c.ForcedSaleValue?.Amount, c.HaircutPercentage,
        c.AcceptableValue?.Amount, c.MarketValue?.Currency, c.LastValuationDate, c.NextRevaluationDue,
        c.LienType?.ToString(), c.LienReference, c.LienRegistrationDate, c.IsInsured,
        c.InsurancePolicyNumber, c.InsuredValue?.Amount, c.InsuranceExpiryDate, c.CreatedAt, c.ApprovedAt,
        c.RejectionReason,
        c.Valuations.Select(v => new CollateralValuationDto(v.Id, v.Type.ToString(), v.Status.ToString(),
            v.ValuationDate, v.MarketValue.Amount, v.ForcedSaleValue?.Amount, v.MarketValue.Currency,
            v.ValuerName, v.ValuerCompany, v.ExpiryDate)).ToList(),
        c.Documents.Select(d => new CollateralDocumentDto(d.Id, d.DocumentType, d.FileName,
            d.FileSizeBytes, d.IsVerified, d.UploadedAt)).ToList()
    );
}

public record RecordPerfectionCommand(
    Guid CollateralId,
    LienType LienType,
    string LienReference,
    string RegistrationAuthority,
    DateTime? RegistrationDate
) : IRequest<ApplicationResult<CollateralDto>>;

public class RecordPerfectionHandler : IRequestHandler<RecordPerfectionCommand, ApplicationResult<CollateralDto>>
{
    private readonly ICollateralRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordPerfectionHandler(ICollateralRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<CollateralDto>> Handle(RecordPerfectionCommand request, CancellationToken ct = default)
    {
        var collateral = await _repository.GetByIdWithDetailsAsync(request.CollateralId, ct);
        if (collateral == null)
            return ApplicationResult<CollateralDto>.Failure("Collateral not found");

        var result = collateral.RecordPerfection(request.LienType, request.LienReference, request.RegistrationAuthority, request.RegistrationDate);
        if (result.IsFailure)
            return ApplicationResult<CollateralDto>.Failure(result.Error);

        _repository.Update(collateral);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<CollateralDto>.Success(MapToDto(collateral));
    }

    private static CollateralDto MapToDto(Domain.Aggregates.Collateral.Collateral c) => new(
        c.Id, c.LoanApplicationId, c.CollateralReference, c.Type.ToString(), c.Status.ToString(),
        c.PerfectionStatus.ToString(), c.Description, c.AssetIdentifier, c.Location, c.OwnerName,
        c.OwnershipType, c.MarketValue?.Amount, c.ForcedSaleValue?.Amount, c.HaircutPercentage,
        c.AcceptableValue?.Amount, c.MarketValue?.Currency, c.LastValuationDate, c.NextRevaluationDue,
        c.LienType?.ToString(), c.LienReference, c.LienRegistrationDate, c.IsInsured,
        c.InsurancePolicyNumber, c.InsuredValue?.Amount, c.InsuranceExpiryDate, c.CreatedAt, c.ApprovedAt,
        c.RejectionReason,
        c.Valuations.Select(v => new CollateralValuationDto(v.Id, v.Type.ToString(), v.Status.ToString(),
            v.ValuationDate, v.MarketValue.Amount, v.ForcedSaleValue?.Amount, v.MarketValue.Currency,
            v.ValuerName, v.ValuerCompany, v.ExpiryDate)).ToList(),
        c.Documents.Select(d => new CollateralDocumentDto(d.Id, d.DocumentType, d.FileName,
            d.FileSizeBytes, d.IsVerified, d.UploadedAt)).ToList()
    );
}
