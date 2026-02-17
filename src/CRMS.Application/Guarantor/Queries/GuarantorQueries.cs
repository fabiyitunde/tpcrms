using CRMS.Application.Common;
using CRMS.Application.Guarantor.DTOs;
using CRMS.Domain.Interfaces;
using G = CRMS.Domain.Aggregates.Guarantor;

namespace CRMS.Application.Guarantor.Queries;

public record GetGuarantorByIdQuery(Guid Id) : IRequest<ApplicationResult<GuarantorDto>>;

public class GetGuarantorByIdHandler : IRequestHandler<GetGuarantorByIdQuery, ApplicationResult<GuarantorDto>>
{
    private readonly IGuarantorRepository _repository;

    public GetGuarantorByIdHandler(IGuarantorRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<GuarantorDto>> Handle(GetGuarantorByIdQuery request, CancellationToken ct = default)
    {
        var guarantor = await _repository.GetByIdWithDetailsAsync(request.Id, ct);
        if (guarantor == null)
            return ApplicationResult<GuarantorDto>.Failure("Guarantor not found");

        return ApplicationResult<GuarantorDto>.Success(MapToDto(guarantor));
    }

    private static GuarantorDto MapToDto(G.Guarantor g) => new(
        g.Id, g.LoanApplicationId, g.GuarantorReference, g.Type.ToString(), g.Status.ToString(),
        g.GuaranteeType.ToString(), g.FullName, g.BVN, g.CompanyName, g.RegistrationNumber,
        g.Email, g.Phone, g.Address, g.RelationshipToApplicant, g.IsDirector, g.IsShareHolder,
        g.ShareholdingPercentage, g.DeclaredNetWorth?.Amount, g.VerifiedNetWorth?.Amount,
        g.Occupation, g.EmployerName, g.MonthlyIncome?.Amount, g.DeclaredNetWorth?.Currency,
        g.GuaranteeLimit?.Amount, g.IsUnlimited, g.GuaranteeStartDate, g.GuaranteeEndDate,
        g.BureauReportId, g.CreditScore, g.CreditScoreGrade, g.CreditCheckDate, g.HasCreditIssues,
        g.CreditIssuesSummary, g.ExistingGuaranteeCount, g.TotalExistingGuarantees?.Amount,
        g.HasSignedGuaranteeAgreement, g.AgreementSignedDate, g.CreatedAt, g.ApprovedAt, g.RejectionReason,
        g.Documents.Select(d => new GuarantorDocumentDto(d.Id, d.DocumentType, d.FileName,
            d.FileSizeBytes, d.IsVerified, d.UploadedAt)).ToList()
    );
}

public record GetGuarantorsByLoanApplicationQuery(Guid LoanApplicationId) : IRequest<ApplicationResult<List<GuarantorSummaryDto>>>;

public class GetGuarantorsByLoanApplicationHandler : IRequestHandler<GetGuarantorsByLoanApplicationQuery, ApplicationResult<List<GuarantorSummaryDto>>>
{
    private readonly IGuarantorRepository _repository;

    public GetGuarantorsByLoanApplicationHandler(IGuarantorRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<GuarantorSummaryDto>>> Handle(GetGuarantorsByLoanApplicationQuery request, CancellationToken ct = default)
    {
        var guarantors = await _repository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);

        var dtos = guarantors.Select(g => new GuarantorSummaryDto(
            g.Id, g.GuarantorReference, g.Type.ToString(), g.Status.ToString(), g.FullName,
            g.BVN, g.CreditScore, g.CreditScoreGrade, g.HasCreditIssues,
            g.GuaranteeLimit?.Amount, g.IsUnlimited, g.CreatedAt
        )).ToList();

        return ApplicationResult<List<GuarantorSummaryDto>>.Success(dtos);
    }
}

public record GetGuarantorsByBVNQuery(string BVN) : IRequest<ApplicationResult<List<GuarantorSummaryDto>>>;

public class GetGuarantorsByBVNHandler : IRequestHandler<GetGuarantorsByBVNQuery, ApplicationResult<List<GuarantorSummaryDto>>>
{
    private readonly IGuarantorRepository _repository;

    public GetGuarantorsByBVNHandler(IGuarantorRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApplicationResult<List<GuarantorSummaryDto>>> Handle(GetGuarantorsByBVNQuery request, CancellationToken ct = default)
    {
        var guarantors = await _repository.GetByBVNAsync(request.BVN, ct);

        var dtos = guarantors.Select(g => new GuarantorSummaryDto(
            g.Id, g.GuarantorReference, g.Type.ToString(), g.Status.ToString(), g.FullName,
            g.BVN, g.CreditScore, g.CreditScoreGrade, g.HasCreditIssues,
            g.GuaranteeLimit?.Amount, g.IsUnlimited, g.CreatedAt
        )).ToList();

        return ApplicationResult<List<GuarantorSummaryDto>>.Success(dtos);
    }
}
