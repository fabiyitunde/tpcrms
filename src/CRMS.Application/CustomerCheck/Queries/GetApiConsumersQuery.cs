using CRMS.Application.Common;
using CRMS.Application.CustomerCheck.Commands;
using CRMS.Application.CustomerCheck.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CustomerCheck.Queries;

public record GetApiConsumersQuery : IRequest<ApplicationResult<List<ApiConsumerDto>>>;

public class GetApiConsumersHandler : IRequestHandler<GetApiConsumersQuery, ApplicationResult<List<ApiConsumerDto>>>
{
    private readonly IApiConsumerRepository _repository;

    public GetApiConsumersHandler(IApiConsumerRepository repository) => _repository = repository;

    public async Task<ApplicationResult<List<ApiConsumerDto>>> Handle(GetApiConsumersQuery request, CancellationToken ct = default)
    {
        var consumers = await _repository.GetAllAsync(ct);
        return ApplicationResult<List<ApiConsumerDto>>.Success(consumers.Select(ApiConsumerMapping.ToDto).ToList());
    }
}
