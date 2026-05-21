using System.Text.Json;
using CRMS.Application.Common;
using CRMS.Application.Namp.DTOs;
using CRMS.Application.Namp.Queries;
using CRMS.Domain.Aggregates.Namp;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Namp.Commands;

/// <summary>
/// Loan Officer recalls a NAMP staging record into a draft NampApplication.
/// The full payload stored in RawPayload is deserialized here and unpacked into
/// NampApplication (extended fields) plus child entities (guarantors, collaterals,
/// financial statements).
/// Routing tier is resolved from NampRoutingConfig based on applicant category + cart total.
/// </summary>
public record RecallNampApplicationCommand(
    Guid StagingRecordId,
    Guid RecalledByUserId
) : IRequest<ApplicationResult<NampApplicationDto>>;

public class RecallNampApplicationHandler
    : IRequestHandler<RecallNampApplicationCommand, ApplicationResult<NampApplicationDto>>
{
    private readonly INampStagingRepository _stagingRepo;
    private readonly INampApplicationRepository _appRepo;
    private readonly INampRoutingConfigRepository _routingRepo;
    private readonly ILoanProductRepository _productRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RecallNampApplicationHandler(
        INampStagingRepository stagingRepo,
        INampApplicationRepository appRepo,
        INampRoutingConfigRepository routingRepo,
        ILoanProductRepository productRepo,
        IUnitOfWork unitOfWork)
    {
        _stagingRepo = stagingRepo;
        _appRepo = appRepo;
        _routingRepo = routingRepo;
        _productRepo = productRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<NampApplicationDto>> Handle(
        RecallNampApplicationCommand request, CancellationToken ct = default)
    {
        var staging = await _stagingRepo.GetByIdAsync(request.StagingRecordId, ct);
        if (staging is null)
            return ApplicationResult<NampApplicationDto>.Failure("Staging record not found.");

        if (staging.IsRecalled)
            return ApplicationResult<NampApplicationDto>.Failure("This record has already been recalled.");

        if (!staging.ResolvedBranchId.HasValue)
            return ApplicationResult<NampApplicationDto>.Failure("Branch has not been resolved for this staging record. Contact admin.");

        // Auto-resolve the NAMP loan product
        var nampProduct = await _productRepo.GetActiveNampProductAsync(ct);
        if (nampProduct is null)
            return ApplicationResult<NampApplicationDto>.Failure(
                "No active NAMP loan product is configured. Please contact the system administrator to set one up.");

        // Resolve routing tier
        var routingConfig = await _routingRepo.ResolveAsync(staging.ApplicantCategory, staging.EquipmentValue, ct);
        if (routingConfig is null)
            return ApplicationResult<NampApplicationDto>.Failure(
                $"No active routing config found for category '{staging.ApplicantCategory}' and value ₦{staging.EquipmentValue:N2}. Please configure routing rules.");

        // Deserialize full payload from staging blob
        RecallPayload? payload = null;
        if (!string.IsNullOrWhiteSpace(staging.RawPayload))
        {
            try
            {
                payload = JsonSerializer.Deserialize<RecallPayload>(staging.RawPayload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
            }
            catch (JsonException)
            {
                // Non-fatal — proceed with staging fields only; extended info will be null
            }
        }

        var createResult = NampApplication.Create(
            applicationNumber: staging.CrmsApplicationNumber,
            loanProductId: nampProduct.Id,
            applicationReference: staging.ApplicationReference,
            applicantName: staging.ApplicantName,
            boaAccountNumber: staging.BoaAccountNumber,
            applicantCategory: staging.ApplicantCategory,
            equipmentDescription: staging.EquipmentDescription,
            equipmentValue: staging.EquipmentValue,
            branchId: staging.ResolvedBranchId.Value,
            committeeTier: routingConfig.CommitteeTier,
            recalledByUserId: request.RecalledByUserId,
            officeId: staging.ResolvedOfficeId,
            locationId: staging.ResolvedLocationId,
            applicantPhone: staging.ApplicantPhone,
            applicantEmail: staging.ApplicantEmail);

        if (createResult.IsFailure)
            return ApplicationResult<NampApplicationDto>.Failure(createResult.Error);

        var app = createResult.Value;

        // Populate extended fields from payload
        if (payload is not null)
        {
            app.SetExtendedApplicantInfo(
                nin: payload.NationalIdNumber,
                bvn: payload.BankVerificationNumber,
                boaAccountName: payload.BoaAccountName,
                loanPurpose: payload.LoanPurpose,
                industrySector: payload.IndustrySector,
                requestedTenorMonths: payload.RequestedTenorMonths,
                dateOfBirth: payload.DateOfBirth,
                stateOfResidence: payload.StateOfResidence,
                localGovernmentArea: payload.LocalGovernmentArea,
                companyName: payload.CompanyName,
                rcNumber: payload.RcNumber);

            app.SetIndividualCreditProfile(
                occupation: payload.Occupation,
                employerName: payload.EmployerName,
                employmentStatus: payload.EmploymentStatus,
                yearsOfExperience: payload.YearsOfExperience,
                numberOfDependants: payload.NumberOfDependants,
                estimatedMonthlyIncome: payload.EstimatedMonthlyIncome,
                otherIncomeSource: payload.OtherIncomeSource,
                otherMonthlyIncome: payload.OtherMonthlyIncome,
                monthlyLivingExpenses: payload.MonthlyLivingExpenses,
                ownedAssetsDescription: payload.OwnedAssetsDescription,
                estimatedNetWorth: payload.EstimatedNetWorth,
                existingLoanObligations: payload.ExistingLoanObligations);

            app.SetLoanAmounts(
                equityPercent: payload.EquityPercent,
                cartTotal: payload.CartTotal,
                equityAmount: payload.EquityAmount,
                loanAmount: payload.LoanAmount);

            if (payload.EquipmentCart?.Count > 0)
                app.SetEquipmentCartJson(JsonSerializer.Serialize(payload.EquipmentCart));

            // Guarantors
            foreach (var g in payload.Guarantors ?? [])
            {
                app.AddGuarantor(NampGuarantor.Create(
                    nampApplicationId: app.Id,
                    fullName: g.FullName ?? string.Empty,
                    bvn: g.Bvn, nin: g.Nin, dateOfBirth: g.DateOfBirth, gender: g.Gender,
                    companyName: g.CompanyName, companyRegistrationNumber: g.CompanyRegistrationNumber,
                    email: g.Email, phone: g.Phone, address: g.Address,
                    guarantorType: g.GuarantorType ?? string.Empty,
                    guaranteeType: g.GuaranteeType ?? string.Empty,
                    relationshipToApplicant: g.RelationshipToApplicant,
                    isDirector: g.IsDirector, isShareholder: g.IsShareholder,
                    shareholdingPercentage: g.ShareholdingPercentage,
                    declaredNetWorth: g.DeclaredNetWorth,
                    occupation: g.Occupation, employerName: g.EmployerName,
                    monthlyIncome: g.MonthlyIncome,
                    guaranteeLimit: g.GuaranteeLimit, isUnlimited: g.IsUnlimited));
            }

            // Collaterals
            foreach (var c in payload.Collaterals ?? [])
            {
                app.AddCollateral(NampCollateral.Create(
                    nampApplicationId: app.Id,
                    collateralType: c.CollateralType ?? string.Empty,
                    description: c.Description ?? string.Empty,
                    assetIdentifier: c.AssetIdentifier, location: c.Location,
                    ownerName: c.OwnerName, ownershipType: c.OwnershipType,
                    marketValue: c.MarketValue, forcedSaleValue: c.ForcedSaleValue,
                    lienType: c.LienType, lienReference: c.LienReference,
                    lienRegistrationAuthority: c.LienRegistrationAuthority,
                    isInsured: c.IsInsured, insurancePolicyNumber: c.InsurancePolicyNumber,
                    insuranceCompany: c.InsuranceCompany, insuredValue: c.InsuredValue,
                    insuranceExpiryDate: c.InsuranceExpiryDate));
            }

            // Origination documents (S3 references from NAMP portal)
            foreach (var doc in payload.Documents ?? [])
            {
                if (string.IsNullOrWhiteSpace(doc.S3Key) || string.IsNullOrWhiteSpace(doc.FileName))
                    continue;

                var docCategory = doc.DocumentType switch
                {
                    "AuditedAccounts"              => NampDocumentCategory.FinancialModel,
                    "BankStatement"                => NampDocumentCategory.CreditReport,
                    "CertificateOfIncorporation"
                        or "CompanyProfile"
                        or "TaxClearanceCertificate"
                        or "ValidIdentification"   => NampDocumentCategory.SupportingDocument,
                    _                              => NampDocumentCategory.General,
                };

                app.UploadDocument(
                    stage: NampDocumentStage.Origination,
                    fileName: doc.FileName,
                    contentType: doc.ContentType ?? "application/octet-stream",
                    fileSize: doc.FileSizeBytes ?? 0,
                    storagePath: doc.S3Key,
                    uploadedByUserId: Guid.Empty,
                    category: docCategory,
                    description: doc.DocumentTypeLabel);
            }

            // Financial Statements (company only)
            foreach (var fs in payload.FinancialStatements ?? [])
            {
                app.AddFinancialStatement(NampFinancialStatement.Create(
                    nampApplicationId: app.Id,
                    financialYear: fs.FinancialYear,
                    yearEndDate: fs.YearEndDate,
                    financialYearType: fs.FinancialYearType ?? "Audited",
                    inputMethod: fs.InputMethod ?? "ExcelUpload",
                    currency: fs.Currency ?? "NGN",
                    revenue: fs.Revenue, otherOperatingIncome: fs.OtherOperatingIncome,
                    costOfSales: fs.CostOfSales, grossProfit: fs.GrossProfit,
                    sellingExpenses: fs.SellingExpenses, administrativeExpenses: fs.AdministrativeExpenses,
                    depreciationAmortization: fs.DepreciationAmortization,
                    otherOperatingExpenses: fs.OtherOperatingExpenses,
                    operatingExpenses: fs.OperatingExpenses, ebitda: fs.Ebitda,
                    interestIncome: fs.InterestIncome, interestExpense: fs.InterestExpense,
                    otherFinanceCosts: fs.OtherFinanceCosts, incomeTaxExpense: fs.IncomeTaxExpense,
                    dividendsDeclared: fs.DividendsDeclared, netProfit: fs.NetProfit,
                    cashAndCashEquivalents: fs.CashAndCashEquivalents, tradeReceivables: fs.TradeReceivables,
                    inventory: fs.Inventory, prepaidExpenses: fs.PrepaidExpenses,
                    otherCurrentAssets: fs.OtherCurrentAssets, totalCurrentAssets: fs.TotalCurrentAssets,
                    propertyPlantEquipment: fs.PropertyPlantEquipment, intangibleAssets: fs.IntangibleAssets,
                    longTermInvestments: fs.LongTermInvestments, deferredTaxAssets: fs.DeferredTaxAssets,
                    otherNonCurrentAssets: fs.OtherNonCurrentAssets, totalNonCurrentAssets: fs.TotalNonCurrentAssets,
                    totalAssets: fs.TotalAssets,
                    tradePayables: fs.TradePayables, shortTermBorrowings: fs.ShortTermBorrowings,
                    currentPortionLongTermDebt: fs.CurrentPortionLongTermDebt, accruedExpenses: fs.AccruedExpenses,
                    taxPayable: fs.TaxPayable, otherCurrentLiabilities: fs.OtherCurrentLiabilities,
                    totalCurrentLiabilities: fs.TotalCurrentLiabilities,
                    longTermDebt: fs.LongTermDebt, deferredTaxLiabilities: fs.DeferredTaxLiabilities,
                    provisions: fs.Provisions, otherNonCurrentLiabilities: fs.OtherNonCurrentLiabilities,
                    totalNonCurrentLiabilities: fs.TotalNonCurrentLiabilities, totalLiabilities: fs.TotalLiabilities,
                    shareCapital: fs.ShareCapital, sharePremium: fs.SharePremium,
                    retainedEarnings: fs.RetainedEarnings, otherReserves: fs.OtherReserves,
                    totalEquity: fs.TotalEquity,
                    cfProfitBeforeTax: fs.CfProfitBeforeTax, cfDepreciationAmortization: fs.CfDepreciationAmortization,
                    cfInterestExpenseAddBack: fs.CfInterestExpenseAddBack,
                    cfChangesInWorkingCapital: fs.CfChangesInWorkingCapital,
                    cfTaxPaid: fs.CfTaxPaid, cfOtherOperatingAdjustments: fs.CfOtherOperatingAdjustments,
                    netCashFromOperating: fs.NetCashFromOperating,
                    cfPurchaseOfPpe: fs.CfPurchaseOfPpe, cfSaleOfPpe: fs.CfSaleOfPpe,
                    cfPurchaseOfInvestments: fs.CfPurchaseOfInvestments, cfSaleOfInvestments: fs.CfSaleOfInvestments,
                    cfInterestReceived: fs.CfInterestReceived, cfDividendsReceived: fs.CfDividendsReceived,
                    cfOtherInvestingActivities: fs.CfOtherInvestingActivities,
                    netCashFromInvesting: fs.NetCashFromInvesting,
                    cfProceedsFromBorrowings: fs.CfProceedsFromBorrowings,
                    cfRepaymentOfBorrowings: fs.CfRepaymentOfBorrowings,
                    cfInterestPaid: fs.CfInterestPaid, cfDividendsPaid: fs.CfDividendsPaid,
                    cfProceedsFromShareIssue: fs.CfProceedsFromShareIssue,
                    cfOtherFinancingActivities: fs.CfOtherFinancingActivities,
                    netCashFromFinancing: fs.NetCashFromFinancing, netChangeInCash: fs.NetChangeInCash,
                    cfOpeningCashBalance: fs.CfOpeningCashBalance, closingCashBalance: fs.ClosingCashBalance,
                    auditorName: fs.AuditorName, auditorFirm: fs.AuditorFirm,
                    auditDate: fs.AuditDate, auditOpinion: fs.AuditOpinion));
            }
        }

        app.SetAuditInfo(request.RecalledByUserId.ToString(), isNew: true);

        staging.MarkRecalled(request.RecalledByUserId);
        _stagingRepo.Update(staging);

        await _appRepo.AddAsync(app, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // Re-fetch with details for the response
        var full = await _appRepo.GetByIdWithDetailsAsync(app.Id, ct);
        return ApplicationResult<NampApplicationDto>.Success(GetNampApplicationByIdHandler.MapToDto(full!));
    }
}

// ── Internal payload classes for deserialization ──────────────────────────────

internal sealed class RecallPayload
{
    public string? NationalIdNumber { get; set; }
    public string? BankVerificationNumber { get; set; }
    public string? BoaAccountName { get; set; }
    public string? LoanPurpose { get; set; }
    public string? IndustrySector { get; set; }
    public int? RequestedTenorMonths { get; set; }
    public string? DateOfBirth { get; set; }
    public string? StateOfResidence { get; set; }
    public string? LocalGovernmentArea { get; set; }
    public string? CompanyName { get; set; }
    public string? RcNumber { get; set; }
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
    public decimal? EquityPercent { get; set; }
    public decimal? CartTotal { get; set; }
    public decimal? EquityAmount { get; set; }
    public decimal? LoanAmount { get; set; }
    public List<RecallGuarantor>? Guarantors { get; set; }
    public List<RecallCollateral>? Collaterals { get; set; }
    public List<RecallFinancialStatement>? FinancialStatements { get; set; }
    public List<object>? EquipmentCart { get; set; }
    public List<RecallDocument>? Documents { get; set; }
}

internal sealed class RecallGuarantor
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

internal sealed class RecallCollateral
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

internal sealed class RecallFinancialStatement
{
    public int FinancialYear { get; set; }
    public string? YearEndDate { get; set; }
    public string? FinancialYearType { get; set; }
    public string? InputMethod { get; set; }
    public string? Currency { get; set; }
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
    public string? AuditorName { get; set; }
    public string? AuditorFirm { get; set; }
    public string? AuditDate { get; set; }
    public string? AuditOpinion { get; set; }
}

internal sealed class RecallDocument
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
