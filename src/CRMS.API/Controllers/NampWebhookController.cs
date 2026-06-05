using System.Text.Json;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.Namp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using CRMS.Domain.Aggregates.Location;

namespace CRMS.API.Controllers;

/// <summary>
/// Receives inbound NAMP application payloads from PAYS / Heifer Nigeria.
/// Auth: X-Api-Key header (constant-time comparison).
/// Returns { applicationId, applicationNumber } on success.
/// </summary>
[ApiController]
[Route("api/v1/namp/webhook")]
public class NampWebhookController : ControllerBase
{
    private readonly INampStagingRepository _stagingRepo;
    private readonly INampApplicationRepository _nampAppRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INampCallbackService _callbackService;
    private readonly IFineractDirectService _fineract;
    private readonly ILocationRepository _locationRepo;
    private readonly NampSettings _settings;
    private readonly ILogger<NampWebhookController> _logger;

    public NampWebhookController(
        INampStagingRepository stagingRepo,
        INampApplicationRepository nampAppRepo,
        IUnitOfWork unitOfWork,
        INampCallbackService callbackService,
        IFineractDirectService fineract,
        ILocationRepository locationRepo,
        IOptions<NampSettings> settings,
        ILogger<NampWebhookController> logger)
    {
        _stagingRepo = stagingRepo;
        _nampAppRepo = nampAppRepo;
        _unitOfWork = unitOfWork;
        _callbackService = callbackService;
        _fineract = fineract;
        _locationRepo = locationRepo;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/v1/namp/webhook/application
    /// Inbound payload from PAYS when an applicant submits a NAMP request.
    /// </summary>
    [HttpPost("application")]
    public async Task<IActionResult> ReceiveApplication([FromBody] NampWebhookPayload? payload, CancellationToken ct)
    {
        // Validate API key
        if (!string.IsNullOrEmpty(_settings.ApiKey))
        {
            Request.Headers.TryGetValue("X-Api-Key", out var providedKey);
            if (!CryptographicEquals(_settings.ApiKey, providedKey.ToString()))
            {
                _logger.LogWarning("NAMP webhook: invalid API key from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
                return Unauthorized("Invalid or missing API key.");
            }
        }

        if (payload is null
            || string.IsNullOrWhiteSpace(payload.ApplicationReference)
            || string.IsNullOrWhiteSpace(payload.BoaAccountNumber)
            || string.IsNullOrWhiteSpace(payload.Category))
        {
            return BadRequest("Missing required fields: applicationReference, boaAccountNumber, category.");
        }

        // Map NAMP category names → CRMS enum (needed for both create and update paths)
        var category = MapCategory(payload.Category);
        if (category is null)
            return BadRequest($"Unknown category: '{payload.Category}'. Expected: YouthEntrepreneur, WomanEntrepreneur, MechanisationCompany.");

        // Deep payload validation — ensures all fields required for the full CRMS processing flow are present
        var validationErrors = NampPayloadValidator.Validate(payload, category.Value);
        if (validationErrors.Count > 0)
        {
            _logger.LogWarning("NAMP webhook: payload validation failed for {Ref} ({Category}) — {Count} error(s): {Errors}",
                payload.ApplicationReference, category.Value, validationErrors.Count, string.Join("; ", validationErrors));
            return UnprocessableEntity(new
            {
                message = $"Payload validation failed for {category.Value} application.",
                errors = validationErrors
            });
        }

        // Serialize full payload as RawPayload blob (needed for both create and update paths)
        var rawPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        // Derive EquipmentDescription from cart items (needed for both paths)
        var equipmentDescription = payload.EquipmentCart?.Count > 0
            ? string.Join(", ", payload.EquipmentCart.Select(e => $"{e.Quantity}× {e.EquipmentName}"))
            : "N/A";

        // Idempotency — if already staged, update payload if not yet recalled, then return stored IDs
        var existing = await _stagingRepo.GetByApplicationReferenceAsync(payload.ApplicationReference, ct);
        if (existing is not null)
        {
            if (!existing.IsRecalled)
            {
                existing.UpdatePayload(
                    rawPayload: rawPayload,
                    applicantName: payload.FullName ?? string.Empty,
                    applicantPhone: payload.Phone,
                    applicantEmail: payload.Email,
                    equipmentDescription: equipmentDescription,
                    equipmentValue: payload.CartTotal,
                    applicantCategory: category.Value);
                _stagingRepo.Update(existing);
                await _unitOfWork.SaveChangesAsync(ct);
                _logger.LogInformation("NAMP webhook: updated staging record for {Ref} (not yet recalled)", payload.ApplicationReference);
            }
            else
            {
                _logger.LogInformation("NAMP webhook: duplicate reference {Ref} already recalled — returning stored IDs", payload.ApplicationReference);
            }
            return Ok(new
            {
                applicationId = existing.Id,
                applicationNumber = existing.CrmsApplicationNumber
            });
        }

        // Resolve BOA account → Fineract client → CRMS branch
        var branchResolution = await ResolveBranchAsync(payload.BoaAccountNumber!, ct);
        if (branchResolution.IsFailure)
        {
            _logger.LogWarning("NAMP webhook: branch resolution failed for account {Account} — {Error}",
                payload.BoaAccountNumber, branchResolution.Error);
            return UnprocessableEntity(branchResolution.Error);
        }

        var resolvedBranch = branchResolution.Value;

        var record = NampStagingRecord.Create(
            applicationReference: payload.ApplicationReference,
            rawPayload: rawPayload,
            applicantName: payload.FullName ?? string.Empty,
            boaAccountNumber: payload.BoaAccountNumber,
            applicantCategory: category.Value,
            equipmentDescription: equipmentDescription,
            equipmentValue: payload.CartTotal,
            applicantPhone: payload.Phone,
            applicantEmail: payload.Email);

        record.ResolveBranch(resolvedBranch.Id, resolvedBranch.ParentLocationId, null);
        record.SetAuditInfo("webhook", isNew: true);

        await _stagingRepo.AddAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("NAMP webhook: staged {Ref} → {AppNumber}", payload.ApplicationReference, record.CrmsApplicationNumber);

        // Non-fatal: send Received callback to PAYS
        await _callbackService.SendCallbackAsync(payload.ApplicationReference, NampCallbackStatus.Received, ct: ct);

        return Ok(new
        {
            applicationId = record.Id,
            applicationNumber = record.CrmsApplicationNumber
        });
    }

    /// <summary>
    /// GET /api/v1/namp/webhook/application/{crmsApplicationNumber}
    /// Portal-facing status endpoint. Returns a friendly payload for any lookup — 404 is never returned.
    /// For applications that have been received but not yet recalled by a loan officer, returns a
    /// basic "Application Received" response sourced from the staging record.
    /// Auth: X-Api-Key header (same key as POST endpoint).
    /// </summary>
    [HttpGet("application/{crmsApplicationNumber}")]
    public async Task<IActionResult> GetApplicationStatus(string crmsApplicationNumber, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_settings.ApiKey))
        {
            Request.Headers.TryGetValue("X-Api-Key", out var providedKey);
            if (!CryptographicEquals(_settings.ApiKey, providedKey.ToString()))
            {
                _logger.LogWarning("NAMP portal status: invalid API key from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
                return Unauthorized("Invalid or missing API key.");
            }
        }

        // Primary lookup: recalled application (full lifecycle data available)
        var app = await _nampAppRepo.GetByApplicationNumberWithHistoryAsync(crmsApplicationNumber, ct);

        if (app is null)
        {
            // Fallback: application received but not yet recalled by a loan officer
            var staged = await _stagingRepo.GetByCrmsApplicationNumberAsync(crmsApplicationNumber, ct);
            if (staged is not null)
            {
                return Ok(new NampPortalStatusResponse
                {
                    ApplicationReference = staged.CrmsApplicationNumber,
                    Found = true,
                    Status = NampPortalStatus.UnderReview,
                    StatusMessage = "Your application has been received and is awaiting assignment to a loan officer.",
                    DeclineReason = null,
                    LastUpdated = staged.ReceivedAt,
                    Applicant = new NampPortalApplicant
                    {
                        FullName = staged.ApplicantName,
                        Bvn = null
                    },
                    ApplicationDetails = new NampPortalApplicationDetails
                    {
                        RequestedAmount = staged.EquipmentValue,
                        RequestedTenorMonths = null,
                        LoanPurpose = staged.EquipmentDescription,
                        SubmittedAt = staged.ReceivedAt
                    },
                    Offer = null,
                    LoanAccount = null,
                    Timeline =
                    [
                        new NampPortalTimelineEntry
                        {
                            Stage = "Application Received",
                            OccurredAt = staged.ReceivedAt,
                            Note = null
                        }
                    ]
                });
            }

            return Ok(new NampPortalStatusResponse
            {
                ApplicationReference = crmsApplicationNumber,
                Found = false,
                StatusMessage = "No application was found with this reference. Please verify the reference number or contact support.",
                Timeline = []
            });
        }

        var portalStatus = MapToPortalStatus(app.Status);
        var statusMessage = BuildStatusMessage(portalStatus, app.Status);
        var declineReason = BuildDeclineReason(app);

        var timeline = app.StatusHistory
            .Select(h => new NampPortalTimelineEntry
            {
                Stage = MapStatusToStageLabel(h.Status),
                OccurredAt = h.ChangedAt,
                Note = h.Note
            })
            .ToList();

        NampPortalOfferDetails? offer = null;
        NampPortalLoanAccountDetails? loanAccount = null;

        if (portalStatus is NampPortalStatus.OfferMade or NampPortalStatus.Processing)
            offer = await BuildOfferDetailsAsync(app, ct);

        if (portalStatus is NampPortalStatus.Active or NampPortalStatus.Closed)
            loanAccount = await BuildLoanAccountDetailsAsync(app, ct);

        return Ok(new NampPortalStatusResponse
        {
            ApplicationReference = app.ApplicationNumber,
            Found = true,
            Status = portalStatus,
            StatusMessage = statusMessage,
            DeclineReason = declineReason,
            LastUpdated = app.StatusHistory.Count > 0 ? app.StatusHistory[^1].ChangedAt : app.CreatedAt,
            Applicant = new NampPortalApplicant
            {
                FullName = app.ApplicantName,
                Bvn = app.Bvn
            },
            ApplicationDetails = new NampPortalApplicationDetails
            {
                RequestedAmount = app.LoanAmount,
                RequestedTenorMonths = app.RequestedTenorMonths,
                LoanPurpose = app.LoanPurpose,
                SubmittedAt = app.SubmittedAt
            },
            Offer = offer,
            LoanAccount = loanAccount,
            Timeline = timeline
        });
    }

    private async Task<NampPortalOfferDetails> BuildOfferDetailsAsync(NampApplication app, CancellationToken ct)
    {
        var interestRate = app.ApprovedInterestRate ?? app.FineractNominalInterestRate ?? 9m;
        decimal? monthlyInstallment = null;
        decimal? totalInterest = null;
        decimal? totalRepayable = null;
        var fineractDataAvailable = false;

        if (app.FineractProductId.HasValue && app.LoanAmount.HasValue && app.RequestedTenorMonths.HasValue)
        {
            try
            {
                var scheduleRequest = new ScheduleCalculationRequest(
                    ProductId: app.FineractProductId.Value,
                    Principal: app.LoanAmount.Value,
                    NumberOfRepayments: app.RequestedTenorMonths.Value,
                    RepaymentEvery: 1,
                    RepaymentFrequencyType: 2,           // Months
                    InterestRatePerPeriod: interestRate / 12m,
                    InterestRateFrequencyType: 2,        // Per month
                    AmortizationType: 1,                 // Equal installments (EMI)
                    InterestType: 0,                     // Declining balance
                    InterestCalculationPeriodType: 1,    // Same as repayment period
                    ExpectedDisbursementDate: DateTime.UtcNow.Date
                );

                var result = await _fineract.CalculateRepaymentScheduleAsync(scheduleRequest, ct);
                if (result.IsSuccess)
                {
                    var schedule = result.Value;
                    var firstInstallment = schedule.Installments.FirstOrDefault(i => i.PeriodNumber == 1);
                    monthlyInstallment = firstInstallment?.TotalDue;
                    totalInterest = schedule.TotalInterest;
                    totalRepayable = schedule.TotalRepayment;
                    fineractDataAvailable = true;
                }
                else
                {
                    _logger.LogWarning("NAMP portal status: Fineract schedule calc failed for {Ref} — {Error}",
                        app.ApplicationReference, result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NAMP portal status: Fineract schedule calc threw for {Ref}", app.ApplicationReference);
            }
        }

        return new NampPortalOfferDetails
        {
            ApprovedAmount = app.LoanAmount,
            ApprovedTenorMonths = app.RequestedTenorMonths,
            InterestRatePerAnnum = interestRate,
            MonthlyInstallment = monthlyInstallment,
            TotalInterest = totalInterest,
            TotalRepayable = totalRepayable,
            FineractDataAvailable = fineractDataAvailable
        };
    }

    private async Task<NampPortalLoanAccountDetails> BuildLoanAccountDetailsAsync(NampApplication app, CancellationToken ct)
    {
        if (!app.FineractLoanId.HasValue)
        {
            return new NampPortalLoanAccountDetails { FineractDataAvailable = false };
        }

        try
        {
            var result = await _fineract.GetLoanDetailAsync(app.FineractLoanId.Value, ct);
            if (result.IsSuccess)
            {
                var detail = result.Value;
                var nextPeriod = detail.RepaymentSchedule.FirstOrDefault(p => !p.Complete);

                return new NampPortalLoanAccountDetails
                {
                    FineractLoanId = app.FineractLoanId.Value,
                    LoanAccountNumber = app.FineractLoanAccountNumber,
                    DisbursedAmount = detail.Summary.PrincipalDisbursed,
                    DisbursedAt = detail.DisbursementDate,
                    OutstandingBalance = detail.Summary.TotalOutstanding,
                    NextInstallmentDueDate = nextPeriod?.DueDate,
                    NextInstallmentAmount = nextPeriod?.TotalDue,
                    ArrearsAmount = detail.Summary.PenaltyChargesOutstanding + detail.Summary.InterestOutstanding + detail.Summary.PrincipalOutstanding > detail.Summary.TotalOutstanding
                        ? 0m
                        : null,
                    IsInArrears = detail.Summary.PenaltyChargesCharged > 0,
                    FineractDataAvailable = true
                };
            }

            _logger.LogWarning("NAMP portal status: GetLoanDetailAsync failed for {Ref} — {Error}",
                app.ApplicationReference, result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NAMP portal status: GetLoanDetailAsync threw for {Ref}", app.ApplicationReference);
        }

        return new NampPortalLoanAccountDetails
        {
            FineractLoanId = app.FineractLoanId,
            LoanAccountNumber = app.FineractLoanAccountNumber,
            FineractDataAvailable = false
        };
    }

    private static NampPortalStatus MapToPortalStatus(NampApplicationStatus status) => status switch
    {
        NampApplicationStatus.Draft
            or NampApplicationStatus.Submitted
            or NampApplicationStatus.TechnicalAppraisal
            or NampApplicationStatus.FinancialAppraisal
            or NampApplicationStatus.BranchCommitteeCirculation
            or NampApplicationStatus.ZonalCommitteeCirculation
            or NampApplicationStatus.RegionalCommitteeCirculation
            or NampApplicationStatus.HOCommitteeCirculation
            or NampApplicationStatus.Ratification
            => NampPortalStatus.UnderReview,

        NampApplicationStatus.TechnicalDeclined
            or NampApplicationStatus.FinancialDeclined
            or NampApplicationStatus.BranchCommitteeDeclined
            or NampApplicationStatus.ZonalCommitteeDeclined
            or NampApplicationStatus.RegionalCommitteeDeclined
            or NampApplicationStatus.HOCommitteeDeclined
            or NampApplicationStatus.RatificationDeclined
            or NampApplicationStatus.Declined
            => NampPortalStatus.NotApproved,

        NampApplicationStatus.OfferGenerated => NampPortalStatus.OfferMade,

        NampApplicationStatus.OfferAccepted
            or NampApplicationStatus.PreDeploymentVerification
            or NampApplicationStatus.Training
            or NampApplicationStatus.Deployment
            => NampPortalStatus.Processing,

        NampApplicationStatus.Active   => NampPortalStatus.Active,
        NampApplicationStatus.Closed   => NampPortalStatus.Closed,
        NampApplicationStatus.OfferLapsed => NampPortalStatus.OfferLapsed,

        _ => NampPortalStatus.UnderReview  // Received, RecallPending — still ingesting
    };

    private static string BuildStatusMessage(NampPortalStatus portalStatus, NampApplicationStatus internalStatus) => portalStatus switch
    {
        NampPortalStatus.UnderReview  => "Your application is currently under review. Our team will assess all information provided before making a decision.",
        NampPortalStatus.NotApproved  => "We regret to inform you that your application was not successful at this time. Please see the reason below or contact your nearest BOA branch for more information.",
        NampPortalStatus.OfferMade    => "Congratulations! Your loan application has been approved. Please review the offer details below and countersign your offer letter to proceed.",
        NampPortalStatus.Processing   => "Your offer has been accepted and your loan is being processed. You will be contacted once the equipment is ready for deployment.",
        NampPortalStatus.Active       => "Your loan is active. Please see the account details below for your repayment schedule information.",
        NampPortalStatus.Closed       => "Your loan has been fully repaid. Thank you for your participation in the NAMP programme.",
        NampPortalStatus.OfferLapsed  => "Your loan offer has lapsed as it was not accepted within the required timeframe. Please contact your nearest BOA branch if you wish to re-apply.",
        _                             => string.Empty
    };

    private static string? BuildDeclineReason(NampApplication app) => app.Status switch
    {
        NampApplicationStatus.TechnicalDeclined
            => string.IsNullOrWhiteSpace(app.TechnicalAppraisalNote) ? null
               : $"Declined at technical appraisal: {app.TechnicalAppraisalNote}",

        NampApplicationStatus.FinancialDeclined
            => string.IsNullOrWhiteSpace(app.FinancialAppraisalNote) ? null
               : $"Declined at financial appraisal: {app.FinancialAppraisalNote}",

        NampApplicationStatus.BranchCommitteeDeclined
            or NampApplicationStatus.ZonalCommitteeDeclined
            or NampApplicationStatus.RegionalCommitteeDeclined
            or NampApplicationStatus.HOCommitteeDeclined
            => string.IsNullOrWhiteSpace(app.CommitteeDecisionNote) ? null
               : $"Declined by credit committee: {app.CommitteeDecisionNote}",

        NampApplicationStatus.RatificationDeclined
            => string.IsNullOrWhiteSpace(app.RatificationDeclineNote) ? null
               : $"Declined at ratification: {app.RatificationDeclineNote}",

        _ => null
    };

    private static string MapStatusToStageLabel(NampApplicationStatus status) => status switch
    {
        NampApplicationStatus.Received                   => "Application Received",
        NampApplicationStatus.RecallPending              => "Pending Assignment",
        NampApplicationStatus.Draft                      => "Application in Progress",
        NampApplicationStatus.Submitted                  => "Application Submitted",
        NampApplicationStatus.TechnicalAppraisal         => "Technical Appraisal",
        NampApplicationStatus.TechnicalDeclined          => "Technical Appraisal — Declined",
        NampApplicationStatus.FinancialAppraisal         => "Financial Appraisal",
        NampApplicationStatus.FinancialDeclined          => "Financial Appraisal — Declined",
        NampApplicationStatus.BranchCommitteeCirculation => "Branch Credit Committee Review",
        NampApplicationStatus.BranchCommitteeDeclined    => "Branch Credit Committee — Declined",
        NampApplicationStatus.ZonalCommitteeCirculation  => "Zonal Credit Committee Review",
        NampApplicationStatus.ZonalCommitteeDeclined     => "Zonal Credit Committee — Declined",
        NampApplicationStatus.RegionalCommitteeCirculation=> "Regional Credit Committee Review",
        NampApplicationStatus.RegionalCommitteeDeclined  => "Regional Credit Committee — Declined",
        NampApplicationStatus.HOCommitteeCirculation     => "Head Office Credit Committee Review",
        NampApplicationStatus.HOCommitteeDeclined        => "Head Office Credit Committee — Declined",
        NampApplicationStatus.Ratification               => "Ratification",
        NampApplicationStatus.RatificationDeclined       => "Ratification — Declined",
        NampApplicationStatus.OfferGenerated             => "Offer Letter Generated",
        NampApplicationStatus.OfferAccepted              => "Offer Accepted",
        NampApplicationStatus.OfferLapsed                => "Offer Lapsed",
        NampApplicationStatus.PreDeploymentVerification  => "Pre-Deployment Verification",
        NampApplicationStatus.Training                   => "Training",
        NampApplicationStatus.Deployment                 => "Equipment Deployment",
        NampApplicationStatus.Active                     => "Loan Activated",
        NampApplicationStatus.Closed                     => "Loan Closed",
        _                                                => status.ToString()
    };

    /// <summary>
    /// Two-step Fineract branch lookup:
    ///   1. GET /core_banking/api/tp/savingsaccounts/byexternalId/{boaAccountNumber} → clientId
    ///   2. GET /clients/{clientId} → officeName
    ///   3. Match officeName → active CRMS Branch location
    /// Returns Failure with a user-facing message if any step cannot resolve.
    /// </summary>
    private async Task<Domain.Common.Result<Location>> ResolveBranchAsync(string boaAccountNumber, CancellationToken ct)
    {
        var accountResult = await _fineract.GetNampBoaAccountAsync(boaAccountNumber, ct);
        if (accountResult.IsFailure)
            return Domain.Common.Result.Failure<Location>(
                $"Could not verify BOA account '{boaAccountNumber}': {accountResult.Error}");

        var clientResult = await _fineract.GetClientByIdAsync(accountResult.Value.ClientId, ct);
        if (clientResult.IsFailure)
            return Domain.Common.Result.Failure<Location>(
                $"Could not retrieve branch for BOA account '{boaAccountNumber}': {clientResult.Error}");

        var branch = await _locationRepo.GetBranchByNameAsync(clientResult.Value.OfficeName, ct);
        if (branch is null)
            return Domain.Common.Result.Failure<Location>(
                $"BOA account '{boaAccountNumber}' belongs to Fineract office '{clientResult.Value.OfficeName}' " +
                $"which does not match any active CRMS branch. Contact system administrator to add this branch.");

        _logger.LogInformation(
            "NAMP webhook: BOA account {Account} resolved to branch '{Branch}' (Id: {BranchId})",
            boaAccountNumber, branch.Name, branch.Id);

        return Domain.Common.Result.Success(branch);
    }

    private static NampApplicantCategory? MapCategory(string category) => category switch
    {
        "YouthEntrepreneur"   => NampApplicantCategory.YouthAgripreneur,
        "WomanEntrepreneur"   => NampApplicantCategory.WomenAgripreneur,
        "MechanisationCompany" => NampApplicantCategory.AgroServiceCompany,
        _ => null
    };

    /// <summary>Constant-time string comparison to prevent timing attacks.</summary>
    private static bool CryptographicEquals(string expected, string provided)
    {
        if (expected.Length != provided.Length) return false;
        int result = 0;
        for (int i = 0; i < expected.Length; i++)
            result |= expected[i] ^ provided[i];
        return result == 0;
    }
}

// ── Webhook Payload DTO ───────────────────────────────────────────────────────

public sealed class NampWebhookPayload
{
    // Application Identity
    public string ApplicationReference { get; set; } = string.Empty;
    public string? SubmittedAt { get; set; }
    public string? Category { get; set; }

    // Applicant / Company Identity
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? NationalIdNumber { get; set; }
    public string? BankVerificationNumber { get; set; }
    public string? StateOfResidence { get; set; }
    public string? LocalGovernmentArea { get; set; }
    public string? DateOfBirth { get; set; }
    public string? CompanyName { get; set; }
    public string? RcNumber { get; set; }

    // BOA Account & Loan Terms
    public string? BoaAccountNumber { get; set; }
    public string? BoaAccountName { get; set; }
    public string? LoanPurpose { get; set; }
    public string? IndustrySector { get; set; }
    public int? RequestedTenorMonths { get; set; }

    // Individual Credit Profile
    public string? Occupation { get; set; }
    public string? EmployerName { get; set; }
    public string? EmploymentStatus { get; set; }
    public int? YearsOfExperience { get; set; }
    public int? NumberOfDependants { get; set; }
    public decimal? EstimatedMonthlyIncome { get; set; }
    public string? OtherIncomeSource { get; set; }
    public decimal? OtherMonthlyIncome { get; set; }
    public decimal? MonthlyLivingExpenses { get; set; }
    public string? OwnedAssetsDescription { get; set; }
    public decimal? EstimatedNetWorth { get; set; }
    public string? ExistingLoanObligations { get; set; }

    // Equity & Loan Amounts
    public decimal EquityPercent { get; set; }
    public decimal CartTotal { get; set; }
    public decimal EquityAmount { get; set; }
    public decimal LoanAmount { get; set; }

    // Collections
    public List<NampGuarantorPayload>? Guarantors { get; set; }
    public List<NampCollateralPayload>? Collaterals { get; set; }
    public List<NampFinancialStatementPayload>? FinancialStatements { get; set; }
    public List<NampEquipmentCartItem>? EquipmentCart { get; set; }
    public List<NampDocumentPayload>? Documents { get; set; }
}

public sealed class NampGuarantorPayload
{
    public string? FullName { get; set; }
    public string? Bvn { get; set; }
    public string? Nin { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyRegistrationNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? GuarantorType { get; set; }
    public string? GuaranteeType { get; set; }
    public string? RelationshipToApplicant { get; set; }
    public bool IsDirector { get; set; }
    public bool IsShareholder { get; set; }
    public decimal? ShareholdingPercentage { get; set; }
    public decimal? DeclaredNetWorth { get; set; }
    public string? Occupation { get; set; }
    public string? EmployerName { get; set; }
    public decimal? MonthlyIncome { get; set; }
    public decimal? GuaranteeLimit { get; set; }
    public bool IsUnlimited { get; set; }
}

public sealed class NampCollateralPayload
{
    public string? CollateralType { get; set; }
    public string? Description { get; set; }
    public string? AssetIdentifier { get; set; }
    public string? Location { get; set; }
    public string? OwnerName { get; set; }
    public string? OwnershipType { get; set; }
    public decimal? MarketValue { get; set; }
    public decimal? ForcedSaleValue { get; set; }
    public string? LienType { get; set; }
    public string? LienReference { get; set; }
    public string? LienRegistrationAuthority { get; set; }
    public bool IsInsured { get; set; }
    public string? InsurancePolicyNumber { get; set; }
    public string? InsuranceCompany { get; set; }
    public decimal? InsuredValue { get; set; }
    public string? InsuranceExpiryDate { get; set; }
}

public sealed class NampFinancialStatementPayload
{
    public int FinancialYear { get; set; }
    public string? YearEndDate { get; set; }
    public string? FinancialYearType { get; set; }
    public string? InputMethod { get; set; }
    public string? Currency { get; set; }
    // Income Statement
    public decimal? Revenue { get; set; }
    public decimal? OtherOperatingIncome { get; set; }
    public decimal? CostOfSales { get; set; }
    public decimal? GrossProfit { get; set; }
    public decimal? SellingExpenses { get; set; }
    public decimal? AdministrativeExpenses { get; set; }
    public decimal? DepreciationAmortization { get; set; }
    public decimal? OtherOperatingExpenses { get; set; }
    public decimal? OperatingExpenses { get; set; }
    public decimal? Ebitda { get; set; }
    public decimal? InterestIncome { get; set; }
    public decimal? InterestExpense { get; set; }
    public decimal? OtherFinanceCosts { get; set; }
    public decimal? IncomeTaxExpense { get; set; }
    public decimal? DividendsDeclared { get; set; }
    public decimal? NetProfit { get; set; }
    // Balance Sheet
    public decimal? CashAndCashEquivalents { get; set; }
    public decimal? TradeReceivables { get; set; }
    public decimal? Inventory { get; set; }
    public decimal? PrepaidExpenses { get; set; }
    public decimal? OtherCurrentAssets { get; set; }
    public decimal? TotalCurrentAssets { get; set; }
    public decimal? PropertyPlantEquipment { get; set; }
    public decimal? IntangibleAssets { get; set; }
    public decimal? LongTermInvestments { get; set; }
    public decimal? DeferredTaxAssets { get; set; }
    public decimal? OtherNonCurrentAssets { get; set; }
    public decimal? TotalNonCurrentAssets { get; set; }
    public decimal? TotalAssets { get; set; }
    public decimal? TradePayables { get; set; }
    public decimal? ShortTermBorrowings { get; set; }
    public decimal? CurrentPortionLongTermDebt { get; set; }
    public decimal? AccruedExpenses { get; set; }
    public decimal? TaxPayable { get; set; }
    public decimal? OtherCurrentLiabilities { get; set; }
    public decimal? TotalCurrentLiabilities { get; set; }
    public decimal? LongTermDebt { get; set; }
    public decimal? DeferredTaxLiabilities { get; set; }
    public decimal? Provisions { get; set; }
    public decimal? OtherNonCurrentLiabilities { get; set; }
    public decimal? TotalNonCurrentLiabilities { get; set; }
    public decimal? TotalLiabilities { get; set; }
    public decimal? ShareCapital { get; set; }
    public decimal? SharePremium { get; set; }
    public decimal? RetainedEarnings { get; set; }
    public decimal? OtherReserves { get; set; }
    public decimal? TotalEquity { get; set; }
    // Cash Flow
    public decimal? CfProfitBeforeTax { get; set; }
    public decimal? CfDepreciationAmortization { get; set; }
    public decimal? CfInterestExpenseAddBack { get; set; }
    public decimal? CfChangesInWorkingCapital { get; set; }
    public decimal? CfTaxPaid { get; set; }
    public decimal? CfOtherOperatingAdjustments { get; set; }
    public decimal? NetCashFromOperating { get; set; }
    public decimal? CfPurchaseOfPpe { get; set; }
    public decimal? CfSaleOfPpe { get; set; }
    public decimal? CfPurchaseOfInvestments { get; set; }
    public decimal? CfSaleOfInvestments { get; set; }
    public decimal? CfInterestReceived { get; set; }
    public decimal? CfDividendsReceived { get; set; }
    public decimal? CfOtherInvestingActivities { get; set; }
    public decimal? NetCashFromInvesting { get; set; }
    public decimal? CfProceedsFromBorrowings { get; set; }
    public decimal? CfRepaymentOfBorrowings { get; set; }
    public decimal? CfInterestPaid { get; set; }
    public decimal? CfDividendsPaid { get; set; }
    public decimal? CfProceedsFromShareIssue { get; set; }
    public decimal? CfOtherFinancingActivities { get; set; }
    public decimal? NetCashFromFinancing { get; set; }
    public decimal? NetChangeInCash { get; set; }
    public decimal? CfOpeningCashBalance { get; set; }
    public decimal? ClosingCashBalance { get; set; }
    // Audit
    public string? AuditorName { get; set; }
    public string? AuditorFirm { get; set; }
    public string? AuditDate { get; set; }
    public string? AuditOpinion { get; set; }
}

public sealed class NampEquipmentCartItem
{
    public string? EquipmentName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class NampDocumentPayload
{
    public string? DocumentType { get; set; }
    public string? DocumentTypeLabel { get; set; }
    public string? FileName { get; set; }
    public string? S3BucketName { get; set; }
    public string? S3Key { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? UploadedAt { get; set; }
}

// ── Portal Status Response DTOs ───────────────────────────────────────────────

public enum NampPortalStatus
{
    UnderReview,
    NotApproved,
    OfferMade,
    Processing,
    Active,
    Closed,
    OfferLapsed,
}

public sealed class NampPortalStatusResponse
{
    public string ApplicationReference { get; set; } = string.Empty;
    public bool Found { get; set; }
    public NampPortalStatus? Status { get; set; }
    public string? StatusMessage { get; set; }
    public string? DeclineReason { get; set; }
    public DateTime? LastUpdated { get; set; }
    public NampPortalApplicant? Applicant { get; set; }
    public NampPortalApplicationDetails? ApplicationDetails { get; set; }
    public NampPortalOfferDetails? Offer { get; set; }
    public NampPortalLoanAccountDetails? LoanAccount { get; set; }
    public List<NampPortalTimelineEntry> Timeline { get; set; } = [];
}

public sealed class NampPortalApplicant
{
    public string? FullName { get; set; }
    public string? Bvn { get; set; }
}

public sealed class NampPortalApplicationDetails
{
    public decimal? RequestedAmount { get; set; }
    public int? RequestedTenorMonths { get; set; }
    public string? LoanPurpose { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

public sealed class NampPortalOfferDetails
{
    public decimal? ApprovedAmount { get; set; }
    public int? ApprovedTenorMonths { get; set; }
    public decimal InterestRatePerAnnum { get; set; }
    public decimal? MonthlyInstallment { get; set; }
    public decimal? TotalInterest { get; set; }
    public decimal? TotalRepayable { get; set; }
    public bool FineractDataAvailable { get; set; }
}

public sealed class NampPortalLoanAccountDetails
{
    public long? FineractLoanId { get; set; }
    public string? LoanAccountNumber { get; set; }
    public decimal? DisbursedAmount { get; set; }
    public DateTime? DisbursedAt { get; set; }
    public decimal? OutstandingBalance { get; set; }
    public DateTime? NextInstallmentDueDate { get; set; }
    public decimal? NextInstallmentAmount { get; set; }
    public decimal? ArrearsAmount { get; set; }
    public bool? IsInArrears { get; set; }
    public bool FineractDataAvailable { get; set; }
}

public sealed class NampPortalTimelineEntry
{
    public string Stage { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? Note { get; set; }
}
