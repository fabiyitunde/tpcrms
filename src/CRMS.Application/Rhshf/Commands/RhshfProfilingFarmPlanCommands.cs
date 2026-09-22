using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

/// <summary>FAC-side farm plan capture during profiling (EOP Review stage). The staff-side
/// equivalents live in RhshfFinancialAppraisalCommands — the Credit Officer can revise whatever the
/// FAC entered, which is the "portal where available, staff override in CRMS" split.</summary>
public record AddRhshfProfilingFarmPlanCommand(
    string Reference, string Crop, decimal Hectares, decimal ExpectedYieldKgPerHectare, decimal ExpectedPricePerKg)
    : IRequest<ApplicationResult>;

public class AddRhshfProfilingFarmPlanHandler : IRequestHandler<AddRhshfProfilingFarmPlanCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public AddRhshfProfilingFarmPlanHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AddRhshfProfilingFarmPlanCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.AddFarmPlanDuringProfiling(
            request.Crop, request.Hectares, request.ExpectedYieldKgPerHectare, request.ExpectedPricePerKg);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

public record RemoveRhshfProfilingFarmPlanCommand(string Reference, Guid PlanId) : IRequest<ApplicationResult>;

public class RemoveRhshfProfilingFarmPlanHandler : IRequestHandler<RemoveRhshfProfilingFarmPlanCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public RemoveRhshfProfilingFarmPlanHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RemoveRhshfProfilingFarmPlanCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.RemoveFarmPlanDuringProfiling(request.PlanId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}

public record RemoveRhshfProfilingDocumentCommand(string Reference, Guid DocumentId) : IRequest<ApplicationResult>;

public class RemoveRhshfProfilingDocumentHandler : IRequestHandler<RemoveRhshfProfilingDocumentCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IUnitOfWork _uow;

    public RemoveRhshfProfilingDocumentHandler(IRhshfCreditProfileRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(RemoveRhshfProfilingDocumentCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = profile.RemoveSupportingDocument(request.DocumentId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
