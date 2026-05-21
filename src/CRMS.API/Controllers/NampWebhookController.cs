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
    private readonly IUnitOfWork _unitOfWork;
    private readonly INampCallbackService _callbackService;
    private readonly ICoreBankingService _coreBanking;
    private readonly IFineractDirectService _fineract;
    private readonly ILocationRepository _locationRepo;
    private readonly NampSettings _settings;
    private readonly ILogger<NampWebhookController> _logger;

    public NampWebhookController(
        INampStagingRepository stagingRepo,
        IUnitOfWork unitOfWork,
        INampCallbackService callbackService,
        ICoreBankingService coreBanking,
        IFineractDirectService fineract,
        ILocationRepository locationRepo,
        IOptions<NampSettings> settings,
        ILogger<NampWebhookController> logger)
    {
        _stagingRepo = stagingRepo;
        _unitOfWork = unitOfWork;
        _callbackService = callbackService;
        _coreBanking = coreBanking;
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
    /// Two-step Fineract branch lookup:
    ///   1. GET /core_banking/api/tp/savingsaccounts/byexternalId/{boaAccountNumber} → clientId
    ///   2. GET /clients/{clientId} → officeName
    ///   3. Match officeName → active CRMS Branch location
    /// Returns Failure with a user-facing message if any step cannot resolve.
    /// </summary>
    private async Task<Domain.Common.Result<Location>> ResolveBranchAsync(string boaAccountNumber, CancellationToken ct)
    {
        var accountResult = await _coreBanking.GetNampBoaAccountAsync(boaAccountNumber, ct);
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
