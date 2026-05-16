using CRMS.Application.Common;
using CRMS.Domain.Aggregates.OfferLetter;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.OfferLetter.Queries;

public record GetOfferLettersByApplicationQuery(Guid LoanApplicationId)
    : IRequest<ApplicationResult<List<OfferLetterSummaryDto>>>;

public record OfferLetterSummaryDto(
    Guid Id,
    int Version,
    string FileName,
    long FileSizeBytes,
    string Status,
    string GeneratedByUserName,
    DateTime GeneratedAt,
    string StoragePath
);

public class GetOfferLettersByApplicationHandler
    : IRequestHandler<GetOfferLettersByApplicationQuery, ApplicationResult<List<OfferLetterSummaryDto>>>
{
    private readonly IOfferLetterRepository _repo;

    public GetOfferLettersByApplicationHandler(IOfferLetterRepository repo)
    {
        _repo = repo;
    }

    public async Task<ApplicationResult<List<OfferLetterSummaryDto>>> Handle(
        GetOfferLettersByApplicationQuery request, CancellationToken ct = default)
    {
        var letters = (await _repo.GetAllByLoanApplicationIdAsync(request.LoanApplicationId, ct))
            .Where(l => l.Status != OfferLetterStatus.Failed)
            .ToList();

        var dtos = letters.Select(l => new OfferLetterSummaryDto(
            Id: l.Id,
            Version: l.Version,
            FileName: l.FileName,
            FileSizeBytes: l.FileSizeBytes,
            Status: l.Status.ToString(),
            GeneratedByUserName: l.GeneratedByUserName,
            GeneratedAt: l.GeneratedAt,
            StoragePath: l.StoragePath
        )).ToList();

        return ApplicationResult<List<OfferLetterSummaryDto>>.Success(dtos);
    }
}
