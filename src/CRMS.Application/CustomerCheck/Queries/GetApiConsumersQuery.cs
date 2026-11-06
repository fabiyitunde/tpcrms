using CRMS.Application.Common;
using CRMS.Application.CustomerCheck.Commands;
using CRMS.Application.CustomerCheck.DTOs;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CustomerCheck.Queries;

public record GetApiConsumersQuery : IRequest<ApplicationResult<List<ApiConsumerDto>>>;

public class GetApiConsumersHandler : IRequestHandler<GetApiConsumersQuery, ApplicationResult<List<ApiConsumerDto>>>
{
    private readonly IApiConsumerRepository _repository;
    private readonly IApiConsumerSetupLinkRepository _links;

    public GetApiConsumersHandler(IApiConsumerRepository repository, IApiConsumerSetupLinkRepository links)
    {
        _repository = repository;
        _links = links;
    }

    public async Task<ApplicationResult<List<ApiConsumerDto>>> Handle(GetApiConsumersQuery request, CancellationToken ct = default)
    {
        var consumers = await _repository.GetAllAsync(ct);
        var now = DateTime.UtcNow;
        var latest = (await _links.GetLatestPerConsumerAsync(ct))
            .GroupBy(l => l.ConsumerId)
            .ToDictionary(g => g.Key, g => ApiConsumerSetupLinks.ToDto(g.OrderByDescending(l => l.CreatedAt).First(), now));
        return ApplicationResult<List<ApiConsumerDto>>.Success(
            consumers.Select(c => ApiConsumerMapping.ToDto(c, latest.GetValueOrDefault(c.Id))).ToList());
    }
}
