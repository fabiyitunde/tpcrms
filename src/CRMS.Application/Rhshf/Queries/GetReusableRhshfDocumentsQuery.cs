using CRMS.Application.Common;
using CRMS.Application.Rhshf.DTOs;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Rhshf.Queries;

/// <summary>
/// Documents in one category that this FAC supplied on its earlier cases, offered on the documents
/// stage as an alternative to uploading the file again. Nothing is filtered on age: the programme
/// defines no point at which a document goes stale, so each candidate is shown with where it came
/// from and when, and the FAC chooses.
/// </summary>
public record GetReusableRhshfDocumentsQuery(string Reference, RhshfDocumentCategory Category)
    : IRequest<ApplicationResult<List<RhshfReusableDocumentDto>>>;

public class GetReusableRhshfDocumentsHandler
    : IRequestHandler<GetReusableRhshfDocumentsQuery, ApplicationResult<List<RhshfReusableDocumentDto>>>
{
    private readonly IRhshfCreditProfileRepository _repo;

    public GetReusableRhshfDocumentsHandler(IRhshfCreditProfileRepository repo)
    {
        _repo = repo;
    }

    public async Task<ApplicationResult<List<RhshfReusableDocumentDto>>> Handle(
        GetReusableRhshfDocumentsQuery request, CancellationToken ct = default)
    {
        var profile = await _repo.GetByReferenceAsync(request.Reference, ct);
        if (profile is null)
            return ApplicationResult<List<RhshfReusableDocumentDto>>.Failure("Case not found.");

        var candidates = await _repo.GetReusableDocumentsForFacAsync(
            profile.FacId, profile.Id, request.Category, ct);

        var dtos = candidates
            .Select(c => new RhshfReusableDocumentDto(
                c.DocumentId, c.FileName, c.SizeBytes, c.UploadedAt,
                c.SourceReference, c.SourceSessionName, c.SourceStatus))
            .ToList();

        return ApplicationResult<List<RhshfReusableDocumentDto>>.Success(dtos);
    }
}
