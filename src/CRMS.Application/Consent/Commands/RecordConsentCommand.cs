using CRMS.Application.Common;
using CRMS.Domain.Aggregates.Consent;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Consent.Commands;

public record RecordConsentCommand(
    string SubjectName,
    string? BVN,
    string? BusinessIdentifier,
    ConsentType ConsentType,
    string Purpose,
    string ConsentText,
    ConsentCaptureMethod CaptureMethod,
    Guid CapturedByUserId,
    string CapturedByUserName,
    Guid? LoanApplicationId = null,
    string? Email = null,
    string? PhoneNumber = null,
    string? SignatureData = null,
    string? IpAddress = null,
    string? UserAgent = null,
    int ValidityDays = 365
) : IRequest<ApplicationResult<ConsentRecordDto>>;

public record ConsentRecordDto(
    Guid Id,
    string SubjectName,
    string? SubjectIdentifier,
    ConsentType ConsentType,
    ConsentStatus Status,
    DateTime ConsentGivenAt,
    DateTime ExpiresAt
);

public class RecordConsentHandler : IRequestHandler<RecordConsentCommand, ApplicationResult<ConsentRecordDto>>
{
    private readonly IConsentRecordRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordConsentHandler(IConsentRecordRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<ConsentRecordDto>> Handle(RecordConsentCommand request, CancellationToken ct = default)
    {
        // Determine subject identifier (BVN for individuals, BusinessIdentifier/RC for companies)
        var subjectIdentifier = request.BVN ?? request.BusinessIdentifier;
        
        if (string.IsNullOrWhiteSpace(subjectIdentifier))
            return ApplicationResult<ConsentRecordDto>.Failure("Either BVN or Business Identifier is required");

        // Check for existing valid consent
        var existing = await _repository.GetValidConsentAsync(subjectIdentifier, request.ConsentType, ct);
        if (existing != null)
        {
            // Return existing consent instead of creating duplicate
            return ApplicationResult<ConsentRecordDto>.Success(new ConsentRecordDto(
                existing.Id,
                existing.SubjectName,
                existing.BVN ?? existing.NIN,
                existing.ConsentType,
                existing.Status,
                existing.ConsentGivenAt,
                existing.ExpiresAt
            ));
        }

        // Create new consent record
        // For individuals: BVN goes in BVN field
        // For business: RC number goes in NIN field (repurposed)
        var result = ConsentRecord.Create(
            request.SubjectName,
            request.BVN,
            request.ConsentType,
            request.Purpose,
            request.ConsentText,
            "1.0",
            request.CaptureMethod,
            request.CapturedByUserId,
            request.CapturedByUserName,
            request.LoanApplicationId,
            nin: request.BusinessIdentifier,
            request.Email,
            request.PhoneNumber,
            request.SignatureData,
            request.IpAddress,
            request.UserAgent,
            request.ValidityDays
        );

        if (result.IsFailure)
            return ApplicationResult<ConsentRecordDto>.Failure(result.Error);

        var consent = result.Value;
        await _repository.AddAsync(consent, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<ConsentRecordDto>.Success(new ConsentRecordDto(
            consent.Id,
            consent.SubjectName,
            consent.BVN ?? consent.NIN,
            consent.ConsentType,
            consent.Status,
            consent.ConsentGivenAt,
            consent.ExpiresAt
        ));
    }
}

/// <summary>
/// Records consent for all parties in a loan application (directors, signatories, guarantors, and business entity).
/// This is typically called before triggering credit checks.
/// </summary>
public record RecordBulkConsentCommand(
    Guid LoanApplicationId,
    Guid CapturedByUserId,
    string CapturedByUserName,
    string? IpAddress = null,
    string? UserAgent = null
) : IRequest<ApplicationResult<BulkConsentResultDto>>;

public record BulkConsentResultDto(
    int TotalParties,
    int ConsentsCreated,
    int ConsentsExisting,
    List<ConsentRecordDto> Consents
);

public class RecordBulkConsentHandler : IRequestHandler<RecordBulkConsentCommand, ApplicationResult<BulkConsentResultDto>>
{
    private readonly ILoanApplicationRepository _loanAppRepository;
    private readonly IGuarantorRepository _guarantorRepository;
    private readonly IConsentRecordRepository _consentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordBulkConsentHandler(
        ILoanApplicationRepository loanAppRepository,
        IGuarantorRepository guarantorRepository,
        IConsentRecordRepository consentRepository,
        IUnitOfWork unitOfWork)
    {
        _loanAppRepository = loanAppRepository;
        _guarantorRepository = guarantorRepository;
        _consentRepository = consentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<BulkConsentResultDto>> Handle(RecordBulkConsentCommand request, CancellationToken ct = default)
    {
        var loanApp = await _loanAppRepository.GetByIdAsync(request.LoanApplicationId, ct);
        if (loanApp == null)
            return ApplicationResult<BulkConsentResultDto>.Failure("Loan application not found");

        var guarantors = await _guarantorRepository.GetByLoanApplicationIdAsync(request.LoanApplicationId, ct);
        var consents = new List<ConsentRecordDto>();
        var created = 0;
        var existing = 0;

        // Track identifiers processed in this batch to prevent duplicate records when two parties
        // share the same BVN (e.g. a director also listed as signatory). SaveChangesAsync only runs
        // once at the end, so DB queries within the loop cannot see uncommitted records (BUG H-3 fix).
        var processedInBatch = new Dictionary<string, ConsentRecordDto>(StringComparer.OrdinalIgnoreCase);

        const string consentText = "I hereby authorize the bank to obtain my credit report from any licensed credit bureau in Nigeria for the purpose of credit assessment.";
        const string businessConsentText = "The company hereby authorizes the bank to obtain its credit report from any licensed credit bureau in Nigeria for the purpose of credit assessment.";

        // Process individual parties (directors, signatories)
        foreach (var party in loanApp.Parties.Where(p => !string.IsNullOrEmpty(p.BVN)))
        {
            if (processedInBatch.TryGetValue(party.BVN!, out var cached))
            {
                existing++;
                consents.Add(cached);
                continue;
            }

            var dtoResult = await CreateOrGetConsent(
                party.FullName, party.BVN!, null, ConsentType.CreditBureauCheck,
                "Credit assessment for loan application", consentText,
                request, party.Email, party.PhoneNumber, ct);

            if (!dtoResult.Success)
                return ApplicationResult<BulkConsentResultDto>.Failure(dtoResult.Error!);

            if (dtoResult.IsNew) created++; else existing++;
            consents.Add(dtoResult.Consent!);
            processedInBatch[party.BVN!] = dtoResult.Consent!;
        }

        // Process guarantors
        foreach (var guarantor in guarantors.Where(g => !string.IsNullOrEmpty(g.BVN)))
        {
            if (processedInBatch.TryGetValue(guarantor.BVN!, out var cached))
            {
                existing++;
                consents.Add(cached);
                continue;
            }

            var dtoResult = await CreateOrGetConsent(
                guarantor.FullName, guarantor.BVN!, null, ConsentType.CreditBureauCheck,
                "Guarantor credit assessment for loan application", consentText,
                request, guarantor.Email, guarantor.Phone, ct);

            if (!dtoResult.Success)
                return ApplicationResult<BulkConsentResultDto>.Failure(dtoResult.Error!);

            if (dtoResult.IsNew) created++; else existing++;
            consents.Add(dtoResult.Consent!);
            processedInBatch[guarantor.BVN!] = dtoResult.Consent!;
        }

        // Process business entity (if RC number exists)
        if (!string.IsNullOrEmpty(loanApp.RegistrationNumber))
        {
            var dtoResult = await CreateOrGetConsent(
                loanApp.CustomerName, null, loanApp.RegistrationNumber, ConsentType.CreditBureauCheck,
                "Corporate credit assessment for loan application", businessConsentText,
                request, null, null, ct);

            if (!dtoResult.Success)
                return ApplicationResult<BulkConsentResultDto>.Failure(dtoResult.Error!);

            if (dtoResult.IsNew) created++; else existing++;
            consents.Add(dtoResult.Consent!);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return ApplicationResult<BulkConsentResultDto>.Success(new BulkConsentResultDto(
            consents.Count,
            created,
            existing,
            consents
        ));
    }

    private async Task<(bool Success, string? Error, ConsentRecordDto? Consent, bool IsNew)> CreateOrGetConsent(
        string subjectName,
        string? bvn,
        string? businessIdentifier,
        ConsentType consentType,
        string purpose,
        string consentText,
        RecordBulkConsentCommand request,
        string? email,
        string? phoneNumber,
        CancellationToken ct)
    {
        var subjectIdentifier = bvn ?? businessIdentifier!;
        var existingConsent = await _consentRepository.GetValidConsentAsync(subjectIdentifier, consentType, ct);

        if (existingConsent != null)
        {
            return (true, null, new ConsentRecordDto(
                existingConsent.Id,
                existingConsent.SubjectName,
                existingConsent.BVN ?? existingConsent.NIN,
                existingConsent.ConsentType,
                existingConsent.Status,
                existingConsent.ConsentGivenAt,
                existingConsent.ExpiresAt
            ), false);
        }

        var result = ConsentRecord.Create(
            subjectName, bvn, consentType, purpose, consentText,
            "1.0", ConsentCaptureMethod.Digital,
            request.CapturedByUserId, request.CapturedByUserName,
            request.LoanApplicationId,
            nin: businessIdentifier,
            email, phoneNumber, null,
            request.IpAddress, request.UserAgent, 365);

        // Return a structured failure instead of throwing — keeps the ApplicationResult pattern (BUG H-1 fix)
        if (result.IsFailure)
            return (false, $"Failed to create consent for '{subjectName}': {result.Error}", null, false);

        await _consentRepository.AddAsync(result.Value, ct);

        return (true, null, new ConsentRecordDto(
            result.Value.Id,
            result.Value.SubjectName,
            result.Value.BVN ?? result.Value.NIN,
            result.Value.ConsentType,
            result.Value.Status,
            result.Value.ConsentGivenAt,
            result.Value.ExpiresAt
        ), true);
    }
}
