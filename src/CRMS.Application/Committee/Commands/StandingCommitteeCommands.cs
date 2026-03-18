using CRMS.Application.Common;
using CRMS.Application.Committee.DTOs;
using CRMS.Domain.Aggregates.Committee;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Committee.Commands;

public record CreateStandingCommitteeCommand(
    string Name,
    CommitteeType CommitteeType,
    int RequiredVotes,
    int MinimumApprovalVotes,
    int DefaultDeadlineHours,
    decimal MinAmountThreshold,
    decimal? MaxAmountThreshold
) : IRequest<ApplicationResult<StandingCommitteeDto>>;

public class CreateStandingCommitteeHandler : IRequestHandler<CreateStandingCommitteeCommand, ApplicationResult<StandingCommitteeDto>>
{
    private readonly IStandingCommitteeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateStandingCommitteeHandler(IStandingCommitteeRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<StandingCommitteeDto>> Handle(CreateStandingCommitteeCommand request, CancellationToken ct = default)
    {
        var existing = await _repository.GetByCommitteeTypeAsync(request.CommitteeType, ct);
        if (existing != null)
            return ApplicationResult<StandingCommitteeDto>.Failure($"A standing committee already exists for type {request.CommitteeType}");

        var result = StandingCommittee.Create(
            request.Name, request.CommitteeType,
            request.RequiredVotes, request.MinimumApprovalVotes,
            request.DefaultDeadlineHours,
            request.MinAmountThreshold, request.MaxAmountThreshold);

        if (result.IsFailure)
            return ApplicationResult<StandingCommitteeDto>.Failure(result.Error);

        await _repository.AddAsync(result.Value, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<StandingCommitteeDto>.Success(MapToDto(result.Value));
    }

    internal static StandingCommitteeDto MapToDto(StandingCommittee c) => new(
        c.Id, c.Name, c.CommitteeType.ToString(),
        c.RequiredVotes, c.MinimumApprovalVotes, c.DefaultDeadlineHours,
        c.MinAmountThreshold, c.MaxAmountThreshold, c.IsActive,
        c.Members.Select(m => new StandingCommitteeMemberDto(m.Id, m.UserId, m.UserName, m.Role, m.IsChairperson)).ToList()
    );
}

public record UpdateStandingCommitteeCommand(
    Guid Id,
    string Name,
    int RequiredVotes,
    int MinimumApprovalVotes,
    int DefaultDeadlineHours,
    decimal MinAmountThreshold,
    decimal? MaxAmountThreshold
) : IRequest<ApplicationResult<StandingCommitteeDto>>;

public class UpdateStandingCommitteeHandler : IRequestHandler<UpdateStandingCommitteeCommand, ApplicationResult<StandingCommitteeDto>>
{
    private readonly IStandingCommitteeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateStandingCommitteeHandler(IStandingCommitteeRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<StandingCommitteeDto>> Handle(UpdateStandingCommitteeCommand request, CancellationToken ct = default)
    {
        var committee = await _repository.GetByIdAsync(request.Id, ct);
        if (committee == null)
            return ApplicationResult<StandingCommitteeDto>.Failure("Standing committee not found");

        var result = committee.Update(
            request.Name, request.RequiredVotes, request.MinimumApprovalVotes,
            request.DefaultDeadlineHours, request.MinAmountThreshold, request.MaxAmountThreshold);

        if (result.IsFailure)
            return ApplicationResult<StandingCommitteeDto>.Failure(result.Error);

        _repository.Update(committee);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<StandingCommitteeDto>.Success(CreateStandingCommitteeHandler.MapToDto(committee));
    }
}

public record ToggleStandingCommitteeCommand(Guid Id, bool Activate) : IRequest<ApplicationResult>;

public class ToggleStandingCommitteeHandler : IRequestHandler<ToggleStandingCommitteeCommand, ApplicationResult>
{
    private readonly IStandingCommitteeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ToggleStandingCommitteeHandler(IStandingCommitteeRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(ToggleStandingCommitteeCommand request, CancellationToken ct = default)
    {
        var committee = await _repository.GetByIdAsync(request.Id, ct);
        if (committee == null)
            return ApplicationResult.Failure("Standing committee not found");

        if (request.Activate) committee.Activate();
        else committee.Deactivate();

        _repository.Update(committee);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record AddStandingCommitteeMemberCommand(
    Guid CommitteeId, Guid UserId, string UserName, string Role, bool IsChairperson
) : IRequest<ApplicationResult<StandingCommitteeDto>>;

public class AddStandingCommitteeMemberHandler : IRequestHandler<AddStandingCommitteeMemberCommand, ApplicationResult<StandingCommitteeDto>>
{
    private readonly IStandingCommitteeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AddStandingCommitteeMemberHandler(IStandingCommitteeRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<StandingCommitteeDto>> Handle(AddStandingCommitteeMemberCommand request, CancellationToken ct = default)
    {
        var committee = await _repository.GetByIdAsync(request.CommitteeId, ct);
        if (committee == null)
            return ApplicationResult<StandingCommitteeDto>.Failure("Standing committee not found");

        var result = committee.AddMember(request.UserId, request.UserName, request.Role, request.IsChairperson);
        if (result.IsFailure)
            return ApplicationResult<StandingCommitteeDto>.Failure(result.Error);

        _repository.Update(committee);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<StandingCommitteeDto>.Success(CreateStandingCommitteeHandler.MapToDto(committee));
    }
}

public record RemoveStandingCommitteeMemberCommand(Guid CommitteeId, Guid UserId) : IRequest<ApplicationResult<StandingCommitteeDto>>;

public class RemoveStandingCommitteeMemberHandler : IRequestHandler<RemoveStandingCommitteeMemberCommand, ApplicationResult<StandingCommitteeDto>>
{
    private readonly IStandingCommitteeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveStandingCommitteeMemberHandler(IStandingCommitteeRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<StandingCommitteeDto>> Handle(RemoveStandingCommitteeMemberCommand request, CancellationToken ct = default)
    {
        var committee = await _repository.GetByIdAsync(request.CommitteeId, ct);
        if (committee == null)
            return ApplicationResult<StandingCommitteeDto>.Failure("Standing committee not found");

        var result = committee.RemoveMember(request.UserId);
        if (result.IsFailure)
            return ApplicationResult<StandingCommitteeDto>.Failure(result.Error);

        _repository.Update(committee);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<StandingCommitteeDto>.Success(CreateStandingCommitteeHandler.MapToDto(committee));
    }
}
