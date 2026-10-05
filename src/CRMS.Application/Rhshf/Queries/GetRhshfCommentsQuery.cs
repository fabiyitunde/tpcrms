using CRMS.Application.Common;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

public record GetRhshfCommentsQuery(string Reference) : IRequest<ApplicationResult<List<RhshfCommentDto>>>;

/// <summary>One comment on a case. <see cref="AuthorName"/> is the resolved display name (never a raw
/// id), mirroring how every other RH-SHF tab attributes an actor.</summary>
public record RhshfCommentDto(Guid Id, Guid AuthorUserId, string AuthorName, string Content, DateTime CreatedAt);

public class GetRhshfCommentsHandler : IRequestHandler<GetRhshfCommentsQuery, ApplicationResult<List<RhshfCommentDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCommentRepository _comments;
    private readonly IUserNameResolver _names;

    public GetRhshfCommentsHandler(
        IRhshfCreditProfileRepository repo, IRhshfCommentRepository comments, IUserNameResolver names)
    {
        _repo = repo;
        _comments = comments;
        _names = names;
    }

    public async Task<ApplicationResult<List<RhshfCommentDto>>> Handle(GetRhshfCommentsQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfCommentDto>>.Failure("Case not found.");

        var comments = await _comments.GetByProfileIdAsync(profile.Id, ct);
        var names = await _names.ResolveManyAsync(comments.Select(c => c.AuthorUserId), ct);

        var dtos = comments.Select(c => new RhshfCommentDto(
            c.Id,
            c.AuthorUserId,
            names.TryGetValue(c.AuthorUserId, out var n) ? n : "—",
            c.Content,
            c.CreatedAt)).ToList();

        return ApplicationResult<List<RhshfCommentDto>>.Success(dtos);
    }
}
