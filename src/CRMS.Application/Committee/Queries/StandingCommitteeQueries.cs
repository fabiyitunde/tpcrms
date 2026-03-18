using CRMS.Application.Common;
using CRMS.Application.Committee.DTOs;
using CRMS.Application.Committee.Commands;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Committee.Queries;

public record GetAllStandingCommitteesQuery(bool IncludeInactive = false) : IRequest<ApplicationResult<List<StandingCommitteeDto>>>;

public class GetAllStandingCommitteesHandler : IRequestHandler<GetAllStandingCommitteesQuery, ApplicationResult<List<StandingCommitteeDto>>>
{
    private readonly IStandingCommitteeRepository _repository;

    public GetAllStandingCommitteesHandler(IStandingCommitteeRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<StandingCommitteeDto>>> Handle(GetAllStandingCommitteesQuery request, CancellationToken ct = default)
    {
        var committees = await _repository.GetAllAsync(request.IncludeInactive, ct);
        var dtos = committees.Select(CreateStandingCommitteeHandler.MapToDto).ToList();
        return ApplicationResult<List<StandingCommitteeDto>>.Success(dtos);
    }
}

public record GetStandingCommitteeForAmountQuery(decimal Amount) : IRequest<ApplicationResult<StandingCommitteeDto>>;

public class GetStandingCommitteeForAmountHandler : IRequestHandler<GetStandingCommitteeForAmountQuery, ApplicationResult<StandingCommitteeDto>>
{
    private readonly IStandingCommitteeRepository _repository;

    public GetStandingCommitteeForAmountHandler(IStandingCommitteeRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<StandingCommitteeDto>> Handle(GetStandingCommitteeForAmountQuery request, CancellationToken ct = default)
    {
        var committee = await _repository.GetForAmountAsync(request.Amount, ct);
        if (committee == null)
            return ApplicationResult<StandingCommitteeDto>.Failure("No standing committee configured for this amount");

        return ApplicationResult<StandingCommitteeDto>.Success(CreateStandingCommitteeHandler.MapToDto(committee));
    }
}
