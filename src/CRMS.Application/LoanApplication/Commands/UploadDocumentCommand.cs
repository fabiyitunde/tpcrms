using CRMS.Application.Common;
using CRMS.Application.LoanApplication.DTOs;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.LoanApplication.Commands;

public record UploadDocumentCommand(
    Guid ApplicationId,
    DocumentCategory Category,
    string FileName,
    string FilePath,
    long FileSize,
    string ContentType,
    Guid UploadedByUserId,
    string? Description
) : IRequest<ApplicationResult<LoanApplicationDocumentDto>>;

public class UploadDocumentHandler : IRequestHandler<UploadDocumentCommand, ApplicationResult<LoanApplicationDocumentDto>>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UploadDocumentHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<LoanApplicationDocumentDto>> Handle(UploadDocumentCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult<LoanApplicationDocumentDto>.Failure("Loan application not found");

        var result = application.AddDocument(
            request.Category,
            request.FileName,
            request.FilePath,
            request.FileSize,
            request.ContentType,
            request.UploadedByUserId,
            request.Description
        );

        if (result.IsFailure)
            return ApplicationResult<LoanApplicationDocumentDto>.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        var doc = result.Value;
        return ApplicationResult<LoanApplicationDocumentDto>.Success(new LoanApplicationDocumentDto(
            doc.Id,
            doc.Category.ToString(),
            doc.Status.ToString(),
            doc.FileName,
            doc.FileSize,
            doc.ContentType,
            doc.Description,
            doc.UploadedAt,
            doc.VerifiedAt,
            doc.RejectionReason
        ));
    }
}

public record VerifyDocumentCommand(Guid ApplicationId, Guid DocumentId, Guid UserId) : IRequest<ApplicationResult>;

public class VerifyDocumentHandler : IRequestHandler<VerifyDocumentCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public VerifyDocumentHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(VerifyDocumentCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var document = application.Documents.FirstOrDefault(d => d.Id == request.DocumentId);
        if (document == null)
            return ApplicationResult.Failure("Document not found");

        var result = document.Verify(request.UserId);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        _repository.Update(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}
