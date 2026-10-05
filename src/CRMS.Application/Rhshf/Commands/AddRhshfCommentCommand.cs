using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Commands;

public record AddRhshfCommentCommand(string Reference, Guid AuthorUserId, string Content) : IRequest<ApplicationResult>;

public class AddRhshfCommentHandler : IRequestHandler<AddRhshfCommentCommand, ApplicationResult>
{
    private readonly IRhshfCreditProfileRepository _repo;
    private readonly IRhshfCommentRepository _comments;
    private readonly IUnitOfWork _uow;

    public AddRhshfCommentHandler(IRhshfCreditProfileRepository repo, IRhshfCommentRepository comments, IUnitOfWork uow)
    {
        _repo = repo;
        _comments = comments;
        _uow = uow;
    }

    public async Task<ApplicationResult> Handle(AddRhshfCommentCommand request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult.Failure("Case not found.");

        var result = RhshfComment.Create(profile.Id, request.AuthorUserId, request.Content);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        result.Value.SetAuditInfo(request.AuthorUserId.ToString(), isNew: true);
        await _comments.AddAsync(result.Value, ct);
        await _uow.SaveChangesAsync(ct);
        return ApplicationResult.Success();
    }
}
