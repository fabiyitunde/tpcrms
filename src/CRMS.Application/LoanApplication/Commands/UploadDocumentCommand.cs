using CRMS.Application.Common;
using CRMS.Application.LoanApplication.DTOs;
using CRMS.Domain.Aggregates.LoanApplication;
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
    private readonly ILoanApplicationDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UploadDocumentHandler(
        ILoanApplicationRepository repository, 
        ILoanApplicationDocumentRepository documentRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _documentRepository = documentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<LoanApplicationDocumentDto>> Handle(UploadDocumentCommand request, CancellationToken ct = default)
    {
        // Just verify application exists - don't load with includes to avoid tracking issues
        var applicationExists = await _repository.ExistsAsync(request.ApplicationId, ct);
        if (!applicationExists)
            return ApplicationResult<LoanApplicationDocumentDto>.Failure("Loan application not found");

        var docResult = LoanApplicationDocument.Create(
            request.ApplicationId,
            request.Category,
            request.FileName,
            request.FilePath,
            request.FileSize,
            request.ContentType,
            request.UploadedByUserId,
            request.Description
        );

        if (docResult.IsFailure)
            return ApplicationResult<LoanApplicationDocumentDto>.Failure(docResult.Error);

        var doc = docResult.Value;
        await _documentRepository.AddAsync(doc, ct);
        await _unitOfWork.SaveChangesAsync(ct);

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

        // No need to call Update() - entity is already tracked
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}

public record RejectDocumentCommand(Guid ApplicationId, Guid DocumentId, Guid UserId, string Reason) : IRequest<ApplicationResult>;

public class RejectDocumentHandler : IRequestHandler<RejectDocumentCommand, ApplicationResult>
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RejectDocumentHandler(ILoanApplicationRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(RejectDocumentCommand request, CancellationToken ct = default)
    {
        var application = await _repository.GetByIdAsync(request.ApplicationId, ct);
        if (application == null)
            return ApplicationResult.Failure("Loan application not found");

        var document = application.Documents.FirstOrDefault(d => d.Id == request.DocumentId);
        if (document == null)
            return ApplicationResult.Failure("Document not found");

        var result = document.Reject(request.UserId, request.Reason);
        if (result.IsFailure)
            return ApplicationResult.Failure(result.Error);

        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult.Success();
    }
}
