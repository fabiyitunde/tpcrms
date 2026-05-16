using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CRMS.Infrastructure.ExternalServices.SmartComply;

public class MockSmartComplyProvider : ISmartComplyProvider
{
    private readonly ILogger<MockSmartComplyProvider> _logger;

    private static readonly Dictionary<string, MockIndividualData> _individualData = new()
    {
        ["22234567890"] = new MockIndividualData(
            Name: "JOHN ADEBAYO",
            Phone: "+2348012345001",
            Gender: "Male",
            DateOfBirth: "1975-03-20",
            TotalLoans: 16, ActiveLoans: 1, ClosedLoans: 15, PerformingLoans: 16,
            DelinquentFacilities: 0, TotalBorrowed: 3761267, TotalOutstanding: 0, TotalOverdue: 0,
            HighestLoanAmount: 1134330, MaxDelinquencyDays: 0,
            Creditors: ["First Bank", "GTBank", "Sterling Bank"],
            FraudRiskScore: 25, FraudRecommendation: "Low risk. Recommend approval."
        ),
        ["22234567891"] = new MockIndividualData(
            Name: "AMINA IBRAHIM",
            Phone: "+2348012345002",
            Gender: "Female",
            DateOfBirth: "1980-07-10",
            TotalLoans: 8, ActiveLoans: 2, ClosedLoans: 6, PerformingLoans: 6,
            DelinquentFacilities: 2, TotalBorrowed: 5500000, TotalOutstanding: 1800000, TotalOverdue: 250000,
            HighestLoanAmount: 2500000, MaxDelinquencyDays: 60,
            Creditors: ["Access Bank", "UBA", "Zenith Bank"],
            FraudRiskScore: 55, FraudRecommendation: "Medium risk. Further review recommended."
        ),
        ["22234567892"] = new MockIndividualData(
            Name: "CHUKWUMA OKONKWO",
            Phone: "+2348012345003",
            Gender: "Male",
            DateOfBirth: "1982-11-05",
            TotalLoans: 12, ActiveLoans: 3, ClosedLoans: 9, PerformingLoans: 8,
            DelinquentFacilities: 4, TotalBorrowed: 15000000, TotalOutstanding: 9500000, TotalOverdue: 3500000,
            HighestLoanAmount: 10000000, MaxDelinquencyDays: 180,
            Creditors: ["Zenith Bank", "FCMB", "Fidelity Bank", "Wema Bank"],
            FraudRiskScore: 78, FraudRecommendation: "High risk. Application requires careful review."
        ),
        ["22212345678"] = new MockIndividualData(
            Name: "OLUWASEUN BAKARE",
            Phone: "+2348098765432",
            Gender: "Male",
            DateOfBirth: "1990-08-15",
            TotalLoans: 4, ActiveLoans: 0, ClosedLoans: 4, PerformingLoans: 4,
            DelinquentFacilities: 0, TotalBorrowed: 800000, TotalOutstanding: 0, TotalOverdue: 0,
            HighestLoanAmount: 500000, MaxDelinquencyDays: 0,
            Creditors: ["Sterling Bank"],
            FraudRiskScore: 15, FraudRecommendation: "Excellent credit profile. Recommend approval."
        ),
        ["22234567893"] = new MockIndividualData(
            Name: "FATIMA BELLO",
            Phone: "+2348012345004",
            Gender: "Female",
            DateOfBirth: "1985-09-15",
            TotalLoans: 3, ActiveLoans: 1, ClosedLoans: 2, PerformingLoans: 3,
            DelinquentFacilities: 0, TotalBorrowed: 1200000, TotalOutstanding: 350000, TotalOverdue: 0,
            HighestLoanAmount: 750000, MaxDelinquencyDays: 0,
            Creditors: ["Access Bank", "Zenith Bank"],
            FraudRiskScore: 20, FraudRecommendation: "Low risk. Good repayment history."
        ),

        // --- Seeded BVNs (matching ComprehensiveDataSeeder.BVNs + NigerianNames, uppercased) ---
        ["22111111111"] = new MockIndividualData(
            Name: "ADEWALE OLUSEGUN",
            Phone: "+2348031452601",
            Gender: "Male",
            DateOfBirth: "1977-04-18",
            TotalLoans: 9, ActiveLoans: 1, ClosedLoans: 8, PerformingLoans: 9,
            DelinquentFacilities: 0, TotalBorrowed: 4823450, TotalOutstanding: 312500, TotalOverdue: 0,
            HighestLoanAmount: 1500000, MaxDelinquencyDays: 0,
            Creditors: ["First Bank", "Access Bank", "Zenith Bank"],
            FraudRiskScore: 18, FraudRecommendation: "Low risk. Consistent repayment behaviour."
        ),
        ["22222222222"] = new MockIndividualData(
            Name: "CHUKWUEMEKA NNAMDI",
            Phone: "+2348054873219",
            Gender: "Male",
            DateOfBirth: "1981-11-02",
            TotalLoans: 11, ActiveLoans: 2, ClosedLoans: 9, PerformingLoans: 10,
            DelinquentFacilities: 1, TotalBorrowed: 9147320, TotalOutstanding: 1875000, TotalOverdue: 210000,
            HighestLoanAmount: 4200000, MaxDelinquencyDays: 38,
            Creditors: ["GTBank", "UBA", "FCMB"],
            FraudRiskScore: 44, FraudRecommendation: "Medium risk. One minor delinquency on record. Further review advised."
        ),
        ["22333333333"] = new MockIndividualData(
            Name: "FATIMA ABDULLAHI",
            Phone: "+2348067234510",
            Gender: "Female",
            DateOfBirth: "1983-06-30",
            TotalLoans: 5, ActiveLoans: 1, ClosedLoans: 4, PerformingLoans: 5,
            DelinquentFacilities: 0, TotalBorrowed: 2310000, TotalOutstanding: 450000, TotalOverdue: 0,
            HighestLoanAmount: 900000, MaxDelinquencyDays: 0,
            Creditors: ["First Bank", "Zenith Bank"],
            FraudRiskScore: 15, FraudRecommendation: "Low risk. Clean credit history. Recommend approval."
        ),
        ["22444444444"] = new MockIndividualData(
            Name: "BLESSING OKAFOR",
            Phone: "+2348079841327",
            Gender: "Female",
            DateOfBirth: "1979-09-14",
            TotalLoans: 13, ActiveLoans: 3, ClosedLoans: 10, PerformingLoans: 11,
            DelinquentFacilities: 2, TotalBorrowed: 12650000, TotalOutstanding: 4780000, TotalOverdue: 890000,
            HighestLoanAmount: 6000000, MaxDelinquencyDays: 75,
            Creditors: ["Access Bank", "Diamond Bank", "Fidelity Bank", "Sterling Bank"],
            FraudRiskScore: 56, FraudRecommendation: "Medium risk. Two delinquent facilities. Collateral coverage recommended."
        ),
        ["22555555555"] = new MockIndividualData(
            Name: "OLUWASEUN ADEYEMI",
            Phone: "+2348023916748",
            Gender: "Male",
            DateOfBirth: "1986-02-22",
            TotalLoans: 7, ActiveLoans: 1, ClosedLoans: 6, PerformingLoans: 7,
            DelinquentFacilities: 0, TotalBorrowed: 3480000, TotalOutstanding: 625000, TotalOverdue: 0,
            HighestLoanAmount: 1200000, MaxDelinquencyDays: 0,
            Creditors: ["GTBank", "Zenith Bank"],
            FraudRiskScore: 21, FraudRecommendation: "Low risk. Good repayment history."
        ),
        ["22666666666"] = new MockIndividualData(
            Name: "UCHE OKONKWO",
            Phone: "+2348048563092",
            Gender: "Male",
            DateOfBirth: "1974-07-07",
            TotalLoans: 17, ActiveLoans: 4, ClosedLoans: 13, PerformingLoans: 12,
            DelinquentFacilities: 5, TotalBorrowed: 22900000, TotalOutstanding: 11340000, TotalOverdue: 4560000,
            HighestLoanAmount: 9500000, MaxDelinquencyDays: 150,
            Creditors: ["Wema Bank", "FCMB", "Fidelity Bank", "Sterling Bank", "Heritage Bank"],
            FraudRiskScore: 74, FraudRecommendation: "High risk. Multiple delinquencies. Application requires thorough review and strong collateral."
        ),
        ["22777777777"] = new MockIndividualData(
            Name: "EMEKA EZE",
            Phone: "+2348091274563",
            Gender: "Male",
            DateOfBirth: "1988-12-11",
            TotalLoans: 6, ActiveLoans: 2, ClosedLoans: 4, PerformingLoans: 5,
            DelinquentFacilities: 1, TotalBorrowed: 5120000, TotalOutstanding: 1560000, TotalOverdue: 135000,
            HighestLoanAmount: 2800000, MaxDelinquencyDays: 29,
            Creditors: ["UBA", "Access Bank"],
            FraudRiskScore: 38, FraudRecommendation: "Low-medium risk. Isolated delinquency. Review repayment capacity."
        ),
        ["22888888888"] = new MockIndividualData(
            Name: "FOLAKE BALOGUN",
            Phone: "+2348015632847",
            Gender: "Female",
            DateOfBirth: "1982-03-19",
            TotalLoans: 8, ActiveLoans: 1, ClosedLoans: 7, PerformingLoans: 8,
            DelinquentFacilities: 0, TotalBorrowed: 3970000, TotalOutstanding: 480000, TotalOverdue: 0,
            HighestLoanAmount: 1750000, MaxDelinquencyDays: 0,
            Creditors: ["First Bank", "Zenith Bank", "GTBank"],
            FraudRiskScore: 17, FraudRecommendation: "Low risk. Excellent repayment track record."
        )
    };

    private static readonly Dictionary<string, MockBusinessData> _businessData = new()
    {
        ["RC123456"] = new MockBusinessData(
            Name: "CAPITALFIELD ASSET MGT LTD",
            Phone: "23408036732620",
            DateOfRegistration: "2003-08-20",
            Address: "ELEGANZA HOUSE, 15B JOSEPH WESLEY STR, LAGOS",
            BusinessType: "Small and Medium Scale Enterprise",
            TotalLoans: 24, ActiveLoans: 7, ClosedLoans: 17, PerformingLoans: 23,
            DelinquentFacilities: 1, TotalBorrowed: 567401372, TotalOutstanding: 19732, TotalOverdue: 19754,
            HighestLoanAmount: 22980000,
            FraudRiskScore: 35, FraudRecommendation: "Low-medium risk. Business has good track record."
        ),
        ["RC654321"] = new MockBusinessData(
            Name: "ACME INDUSTRIES LIMITED",
            Phone: "23408098765432",
            DateOfRegistration: "2010-05-15",
            Address: "45 VICTORIA ISLAND, LAGOS",
            BusinessType: "Limited Liability Company",
            TotalLoans: 9, ActiveLoans: 3, ClosedLoans: 6, PerformingLoans: 6,
            DelinquentFacilities: 3, TotalBorrowed: 135582000, TotalOutstanding: 54931635, TotalOverdue: 54931635,
            HighestLoanAmount: 45194000,
            FraudRiskScore: 62, FraudRecommendation: "Medium risk. Current exposure needs evaluation."
        )
    };

    public MockSmartComplyProvider(ILogger<MockSmartComplyProvider> logger)
    {
        _logger = logger;
    }

    #region Individual Credit Reports

    public Task<Result<SmartComplyIndividualCreditReport>> GetFirstCentralSummaryAsync(string bvn, CancellationToken ct = default)
    {
        return GetMockIndividualReportAsync(bvn, "FirstCentralSummary");
    }

    public Task<Result<SmartComplyIndividualCreditReport>> GetFirstCentralFullAsync(string bvn, CancellationToken ct = default)
    {
        return GetMockIndividualReportAsync(bvn, "FirstCentralFull");
    }

    public Task<Result<SmartComplyCreditScore>> GetFirstCentralScoreAsync(string bvn, CancellationToken ct = default)
    {
        return GetMockCreditScoreAsync(bvn, "FirstCentral");
    }

    public Task<Result<SmartComplyIndividualCreditReport>> GetCreditRegistrySummaryAsync(string bvn, CancellationToken ct = default)
    {
        return GetMockIndividualReportAsync(bvn, "CreditRegistrySummary");
    }

    public Task<Result<SmartComplyIndividualCreditReport>> GetCreditRegistryFullAsync(string bvn, CancellationToken ct = default)
    {
        return GetMockIndividualReportAsync(bvn, "CreditRegistryFull");
    }

    public Task<Result<SmartComplyIndividualCreditReport>> GetCreditRegistryAdvancedAsync(string bvn, CancellationToken ct = default)
    {
        return GetMockIndividualReportAsync(bvn, "CreditRegistryAdvanced");
    }

    public Task<Result<SmartComplyCreditScore>> GetCRCScoreAsync(string bvn, CancellationToken ct = default)
    {
        return GetMockCreditScoreAsync(bvn, "CRC");
    }

    public Task<Result<SmartComplyIndividualCreditReport>> GetCRCHistoryAsync(string bvn, CancellationToken ct = default)
    {
        return GetMockIndividualReportAsync(bvn, "CRCHistory");
    }

    public Task<Result<SmartComplyIndividualCreditReport>> GetCRCFullAsync(string bvn, CancellationToken ct = default)
    {
        return GetMockIndividualReportAsync(bvn, "CRCFull");
    }

    public Task<Result<SmartComplyIndividualCreditReport>> GetCreditPremiumAsync(string bvn, CancellationToken ct = default)
    {
        return GetMockIndividualReportAsync(bvn, "CreditPremium");
    }

    private Task<Result<SmartComplyIndividualCreditReport>> GetMockIndividualReportAsync(string bvn, string reportType)
    {
        _logger.LogInformation("[MOCK] Fetching {ReportType} for BVN ****{BvnSuffix}", reportType, bvn.Length >= 4 ? bvn[^4..] : bvn);

        if (!_individualData.TryGetValue(bvn, out var data))
        {
            data = GenerateFallbackIndividualData(bvn);
        }

        var report = new SmartComplyIndividualCreditReport(
            Id: Guid.NewGuid().ToString(),
            Bvn: bvn,
            Name: data.Name,
            Phone: data.Phone,
            Gender: data.Gender,
            DateOfBirth: data.DateOfBirth,
            Address: "Lagos, Nigeria",
            Email: $"{data.Name.ToLower().Replace(" ", ".")}@email.com",
            Summary: new SmartComplyCreditSummary(
                TotalNoOfLoans: data.TotalLoans,
                TotalNoOfInstitutions: data.Creditors.Count,
                TotalNoOfActiveLoans: data.ActiveLoans,
                TotalNoOfClosedLoans: data.ClosedLoans,
                TotalNoOfPerformingLoans: data.PerformingLoans,
                TotalNoOfDelinquentFacilities: data.DelinquentFacilities,
                HighestLoanAmount: data.HighestLoanAmount,
                TotalMonthlyInstallment: data.TotalOutstanding > 0 ? data.TotalOutstanding / 12 : 0,
                TotalBorrowed: data.TotalBorrowed,
                TotalOutstanding: data.TotalOutstanding,
                TotalOverdue: data.TotalOverdue,
                MaxNoOfDays: data.MaxDelinquencyDays,
                ReportOrderNumber: $"SC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}"
            ),
            Creditors: data.Creditors.Select((c, i) => new SmartComplyCreditor(
                SubscriberId: $"CR{100000 + i}",
                Name: c,
                Phone: "+234800000000" + i,
                Address: "Lagos, Nigeria"
            )).ToList(),
            CreditEnquiries: GenerateMockEnquiries(data.Creditors),
            LoanPerformance: GenerateMockLoanPerformance(data),
            LoanHistory: GenerateMockLoanHistory(data),
            SearchedDate: DateTime.UtcNow
        );

        return Task.FromResult(Result.Success(report));
    }

    private Task<Result<SmartComplyCreditScore>> GetMockCreditScoreAsync(string bvn, string provider)
    {
        _logger.LogInformation("[MOCK] Fetching {Provider} credit score for BVN ****{BvnSuffix}", provider, bvn.Length >= 4 ? bvn[^4..] : bvn);

        if (!_individualData.TryGetValue(bvn, out var data))
        {
            data = GenerateFallbackIndividualData(bvn);
        }

        var score = CalculateCreditScore(data);
        var grade = score switch
        {
            >= 750 => "A+",
            >= 700 => "A",
            >= 650 => "B",
            >= 600 => "C",
            >= 550 => "D",
            _ => "E"
        };

        return Task.FromResult(Result.Success(new SmartComplyCreditScore(
            Score: score,
            Grade: grade,
            Provider: provider,
            GeneratedDate: DateTime.UtcNow
        )));
    }

    #endregion

    #region Business Credit Reports

    public Task<Result<SmartComplyBusinessCreditReport>> GetCRCBusinessHistoryAsync(string rcNumber, CancellationToken ct = default)
    {
        return GetMockBusinessReportAsync(rcNumber, "CRCBusinessHistory");
    }

    public Task<Result<SmartComplyBusinessCreditReport>> GetFirstCentralBusinessAsync(string rcNumber, CancellationToken ct = default)
    {
        return GetMockBusinessReportAsync(rcNumber, "FirstCentralBusiness");
    }

    public Task<Result<SmartComplyBusinessCreditReport>> GetPremiumBusinessAsync(string rcNumber, CancellationToken ct = default)
    {
        return GetMockBusinessReportAsync(rcNumber, "PremiumBusiness");
    }

    private Task<Result<SmartComplyBusinessCreditReport>> GetMockBusinessReportAsync(string rcNumber, string reportType)
    {
        _logger.LogInformation("[MOCK] Fetching {ReportType} for RC {RcNumber}", reportType, rcNumber);

        if (!_businessData.TryGetValue(rcNumber.ToUpper(), out var data))
        {
            data = GenerateFallbackBusinessData(rcNumber);
        }

        var report = new SmartComplyBusinessCreditReport(
            Id: Guid.NewGuid().ToString(),
            BusinessRegNo: rcNumber,
            Name: data.Name,
            Phone: data.Phone,
            DateOfRegistration: data.DateOfRegistration,
            Address: data.Address,
            Website: "",
            TaxIdentificationNumber: $"TIN{rcNumber[2..]}",
            NoOfDirectors: 3,
            Industry: "Financial Services",
            BusinessType: data.BusinessType,
            Email: $"info@{GenerateBusinessEmail(data.Name)}",
            Summary: new SmartComplyBusinessCreditSummary(
                TotalNoOfLoans: data.TotalLoans,
                TotalNoOfActiveLoans: data.ActiveLoans,
                TotalNoOfClosedLoans: data.ClosedLoans,
                TotalNoOfInstitutions: 5,
                TotalOverdue: data.TotalOverdue,
                TotalBorrowed: data.TotalBorrowed,
                HighestLoanAmount: data.HighestLoanAmount,
                TotalOutstanding: data.TotalOutstanding,
                TotalMonthlyInstallment: data.TotalOutstanding > 0 ? data.TotalOutstanding / 12 : 0,
                TotalNoOfOverdueAccounts: data.DelinquentFacilities,
                TotalNoOfPerformingLoans: data.PerformingLoans,
                TotalNoOfDelinquentFacilities: data.DelinquentFacilities,
                LastReportedDate: DateTime.UtcNow.AddDays(-30).ToString("dd/MMM/yyyy"),
                ReportOrderNumber: $"SC-BIZ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}"
            ),
            SearchedDate: DateTime.UtcNow
        );

        return Task.FromResult(Result.Success(report));
    }

    #endregion

    #region Loan Fraud Check

    public Task<Result<SmartComplyLoanFraudResult>> CheckIndividualLoanFraudAsync(
        SmartComplyIndividualLoanRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK] Checking individual loan fraud for {Name}", $"{request.FirstName} {request.LastName}");

        var data = _individualData.GetValueOrDefault(request.Bvn);
        var fraudScore = data?.FraudRiskScore ?? 50;
        var recommendation = data?.FraudRecommendation ?? "Unable to assess. Insufficient credit history.";

        // Adjust fraud score based on loan parameters
        // Only apply income-based penalty if income is actually provided (> 0)
        if (request.AnnualIncome > 0 && request.LoanAmountRequested > request.AnnualIncome * 2)
            fraudScore = Math.Min(100, fraudScore + 20);
        if (request.CollateralRequired && request.CollateralValue >= request.LoanAmountRequested)
            fraudScore = Math.Max(0, fraudScore - 10);

        var result = new SmartComplyLoanFraudResult(
            Id: Random.Shared.Next(1000, 9999),
            ApplicantName: $"{request.FirstName} {request.LastName}",
            ApplicantIdentifier: request.Bvn,
            IsIndividual: true,
            IsBusiness: false,
            FraudRiskScore: fraudScore,
            Recommendation: recommendation,
            Status: "reviewed",
            FinancialAnalysis: GenerateMockFinancialAnalysis(request.AnnualIncome, request.LoanAmountRequested, request.CollateralValue ?? 0),
            History: data != null ? new SmartComplyLoanFraudHistory(
                data.TotalLoans, data.Creditors.Count, data.ActiveLoans, data.ClosedLoans,
                data.PerformingLoans, data.DelinquentFacilities, data.HighestLoanAmount,
                data.TotalBorrowed, data.TotalOutstanding, data.TotalOverdue
            ) : null,
            DateCreated: DateTime.UtcNow
        );

        return Task.FromResult(Result.Success(result));
    }

    public Task<Result<SmartComplyLoanFraudResult>> CheckBusinessLoanFraudAsync(
        SmartComplyBusinessLoanRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK] Checking business loan fraud for {Name}", request.BusinessName);

        var data = _businessData.GetValueOrDefault(request.RcNumber.ToUpper());
        var fraudScore = data?.FraudRiskScore ?? 50;
        var recommendation = data?.FraudRecommendation ?? "Unable to assess. Insufficient credit history.";

        // Adjust fraud score based on loan parameters
        // Only apply revenue-based penalty if revenue is actually provided (> 0)
        if (request.AnnualRevenue > 0 && request.LoanAmountRequested > request.AnnualRevenue)
            fraudScore = Math.Min(100, fraudScore + 15);
        if (request.CollateralRequired && request.CollateralValue >= request.LoanAmountRequested)
            fraudScore = Math.Max(0, fraudScore - 10);

        var result = new SmartComplyLoanFraudResult(
            Id: Random.Shared.Next(1000, 9999),
            ApplicantName: request.BusinessName,
            ApplicantIdentifier: request.RcNumber,
            IsIndividual: false,
            IsBusiness: true,
            FraudRiskScore: fraudScore,
            Recommendation: recommendation,
            Status: "reviewed",
            FinancialAnalysis: GenerateMockFinancialAnalysis(request.AnnualRevenue, request.LoanAmountRequested, request.CollateralValue ?? 0),
            History: data != null ? new SmartComplyLoanFraudHistory(
                data.TotalLoans, 5, data.ActiveLoans, data.ClosedLoans,
                data.PerformingLoans, data.DelinquentFacilities, data.HighestLoanAmount,
                data.TotalBorrowed, data.TotalOutstanding, data.TotalOverdue
            ) : null,
            DateCreated: DateTime.UtcNow
        );

        return Task.FromResult(Result.Success(result));
    }

    #endregion

    #region KYC/Identity Verification

    public Task<Result<SmartComplyBvnResult>> VerifyBvnAsync(string bvn, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK] Verifying BVN ****{BvnSuffix}", bvn.Length >= 4 ? bvn[^4..] : bvn);

        if (!_individualData.TryGetValue(bvn, out var data))
        {
            data = GenerateFallbackIndividualData(bvn);
        }

        var nameParts = data.Name.Split(' ');
        return Task.FromResult(Result.Success(new SmartComplyBvnResult(
            FirstName: nameParts.Length > 0 ? nameParts[0] : "",
            MiddleName: nameParts.Length > 2 ? nameParts[1] : null,
            LastName: nameParts.Length > 1 ? nameParts[^1] : "",
            DateOfBirth: data.DateOfBirth,
            PhoneNumber: data.Phone
        )));
    }

    public Task<Result<SmartComplyBvnAdvancedResult>> VerifyBvnAdvancedAsync(string bvn, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK] Verifying BVN Advanced ****{BvnSuffix}", bvn.Length >= 4 ? bvn[^4..] : bvn);

        if (!_individualData.TryGetValue(bvn, out var data))
        {
            data = GenerateFallbackIndividualData(bvn);
        }

        var nameParts = data.Name.Split(' ');
        return Task.FromResult(Result.Success(new SmartComplyBvnAdvancedResult(
            Bvn: bvn,
            Image: null,
            Title: data.Gender == "Male" ? "Mr" : "Mrs",
            Gender: data.Gender.ToLower(),
            FirstName: nameParts.Length > 0 ? nameParts[0] : "",
            MiddleName: nameParts.Length > 2 ? nameParts[1] : null,
            LastName: nameParts.Length > 1 ? nameParts[^1] : "",
            DateOfBirth: data.DateOfBirth,
            PhoneNumber1: data.Phone,
            PhoneNumber2: null,
            MaritalStatus: "Single",
            StateOfOrigin: "Lagos State",
            LgaOfOrigin: "Ikeja",
            StateOfResidence: "Lagos State",
            LgaOfResidence: "Victoria Island",
            ResidentialAddress: "Lagos, Nigeria",
            EnrollmentBank: "033",
            EnrollmentBranch: "Lagos Main Branch",
            RegistrationDate: "2015-01-01",
            LevelOfAccount: "Level 3 - High Level",
            WatchListed: "NO"
        )));
    }

    public Task<Result<SmartComplyNinResult>> VerifyNinAsync(string nin, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK] Verifying NIN");

        return Task.FromResult(Result.Success(new SmartComplyNinResult(
            Nin: nin,
            FirstName: "JOHN",
            MiddleName: "OLUWASEUN",
            LastName: "ADEBAYO",
            DateOfBirth: "1985-03-15",
            Gender: "Male",
            PhoneNumber: "+2348012345678",
            Photo: null
        )));
    }

    public Task<Result<SmartComplyTinResult>> VerifyTinAsync(string tin, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK] Verifying TIN");

        return Task.FromResult(Result.Success(new SmartComplyTinResult(
            TaxIdentificationNumber: tin,
            TaxpayerName: "ACME INDUSTRIES LIMITED",
            CacRegNumber: "RC123456",
            TaxOffice: "MSTO IKEJA",
            PhoneNumber: "+2348012345678",
            Email: "info@acmeindustries.com"
        )));
    }

    public Task<Result<SmartComplyCacResult>> VerifyCacAsync(string rcNumber, CancellationToken ct = default)
    {
        return GetMockCacResultAsync(rcNumber);
    }

    public Task<Result<SmartComplyCacResult>> VerifyCacAdvancedAsync(string rcNumber, string companyName, string companyType = "RC", CancellationToken ct = default)
    {
        return GetMockCacResultAsync(rcNumber);
    }

    private Task<Result<SmartComplyCacResult>> GetMockCacResultAsync(string rcNumber)
    {
        _logger.LogInformation("[MOCK] Verifying CAC for RC {RcNumber}", rcNumber);

        var data = _businessData.GetValueOrDefault(rcNumber.ToUpper());

        return Task.FromResult(Result.Success(new SmartComplyCacResult(
            CompanyName: data?.Name ?? "SAMPLE COMPANY LIMITED",
            RcNumber: rcNumber,
            CompanyType: "Private Limited Company",
            RegistrationDate: data?.DateOfRegistration ?? "2010-01-01",
            Address: data?.Address ?? "Lagos, Nigeria",
            City: "Lagos",
            State: "Lagos",
            Email: "info@company.com",
            Status: "Active",
            NatureOfBusiness: "Financial Services",
            ShareCapital: 10000000,
            CompanyId: null,
            Directors: [
                new SmartComplyCacDirector(
                    Id: 10001, Surname: "ADEBAYO", FirstName: "JOHN", OtherName: "OLUMIDE",
                    FullName: "JOHN OLUMIDE ADEBAYO", Gender: "MALE", DateOfBirth: "1975-03-20",
                    Nationality: "NIGERIAN", Occupation: "BUSINESSMAN", Email: "john.adebayo@company.com",
                    PhoneNumber: "+2348012345001", Address: "45 Victoria Island, Lagos",
                    City: "LAGOS", State: "LAGOS", Lga: "ETI-OSA", Status: "ACTIVE",
                    IsChairman: true, IsCorporate: false, DateOfAppointment: "2010-01-01",
                    AffiliateType: "MANAGING DIRECTOR", TypeOfShares: "ORDINARY",
                    NumSharesAlloted: 400000, IdentityNumber: null, Country: "NIGERIA"
                ),
                new SmartComplyCacDirector(
                    Id: 10002, Surname: "IBRAHIM", FirstName: "AMINA", OtherName: null,
                    FullName: "AMINA IBRAHIM", Gender: "FEMALE", DateOfBirth: "1980-07-10",
                    Nationality: "NIGERIAN", Occupation: "ENTREPRENEUR", Email: "amina.ibrahim@company.com",
                    PhoneNumber: "+2348012345002", Address: "12 Maitama, Abuja",
                    City: "ABUJA", State: "FCT", Lga: "MAITAMA", Status: "ACTIVE",
                    IsChairman: false, IsCorporate: false, DateOfAppointment: "2012-05-15",
                    AffiliateType: "EXECUTIVE DIRECTOR", TypeOfShares: "ORDINARY",
                    NumSharesAlloted: 350000, IdentityNumber: null, Country: "NIGERIA"
                ),
                new SmartComplyCacDirector(
                    Id: 10003, Surname: "OKONKWO", FirstName: "CHUKWUMA", OtherName: "EMEKA",
                    FullName: "CHUKWUMA EMEKA OKONKWO", Gender: "MALE", DateOfBirth: "1982-11-05",
                    Nationality: "NIGERIAN", Occupation: "ENGINEER", Email: "c.okonkwo@company.com",
                    PhoneNumber: "+2348012345003", Address: "3 GRA, Port Harcourt",
                    City: "PORT HARCOURT", State: "RIVERS", Lga: "PORT HARCOURT", Status: "ACTIVE",
                    IsChairman: false, IsCorporate: false, DateOfAppointment: "2015-08-20",
                    AffiliateType: "NON-EXECUTIVE DIRECTOR", TypeOfShares: "ORDINARY",
                    NumSharesAlloted: 250000, IdentityNumber: null, Country: "NIGERIA"
                )
            ]
        )));
    }

    #endregion

    #region Helper Methods

    // Generates deterministic mock data for any BVN not in the hardcoded dictionary.
    // Uses the last digit of the BVN to vary the credit tier, so seeded BVNs get diverse profiles.
    private static MockIndividualData GenerateFallbackIndividualData(string bvn)
    {
        var lastDigit = bvn.Length > 0 && char.IsDigit(bvn[^1]) ? (int)char.GetNumericValue(bvn[^1]) : 0;
        var tier = lastDigit % 3; // 0=good, 1=medium, 2=watch
        var nameIndex = bvn.Sum(c => c) % FallbackMaleNames.Length;
        var name = FallbackMaleNames[nameIndex];

        // Phone: "+234" + "80" + first 8 digits of BVN = valid 10-digit Nigerian local number
        var phone = $"+23480{bvn[1..9]}";
        return tier switch
        {
            0 => new MockIndividualData(
                Name: name, Phone: phone, Gender: "Male", DateOfBirth: "1978-06-12",
                TotalLoans: 6, ActiveLoans: 1, ClosedLoans: 5, PerformingLoans: 6,
                DelinquentFacilities: 0, TotalBorrowed: 2413500, TotalOutstanding: 287000, TotalOverdue: 0,
                HighestLoanAmount: 897000, MaxDelinquencyDays: 0,
                Creditors: ["First Bank", "Zenith Bank"],
                FraudRiskScore: 22, FraudRecommendation: "Low risk. Good repayment history."
            ),
            1 => new MockIndividualData(
                Name: name, Phone: phone, Gender: "Male", DateOfBirth: "1983-09-25",
                TotalLoans: 10, ActiveLoans: 2, ClosedLoans: 8, PerformingLoans: 9,
                DelinquentFacilities: 1, TotalBorrowed: 7436000, TotalOutstanding: 1183000, TotalOverdue: 176500,
                HighestLoanAmount: 2950000, MaxDelinquencyDays: 45,
                Creditors: ["GTBank", "Access Bank", "UBA"],
                FraudRiskScore: 48, FraudRecommendation: "Medium risk. Minor delinquency noted. Further review recommended."
            ),
            _ => new MockIndividualData(
                Name: name, Phone: phone, Gender: "Male", DateOfBirth: "1979-03-08",
                TotalLoans: 14, ActiveLoans: 3, ClosedLoans: 11, PerformingLoans: 10,
                DelinquentFacilities: 4, TotalBorrowed: 17840000, TotalOutstanding: 8390000, TotalOverdue: 3175000,
                HighestLoanAmount: 7850000, MaxDelinquencyDays: 120,
                Creditors: ["Fidelity Bank", "FCMB", "Sterling Bank", "Wema Bank"],
                FraudRiskScore: 71, FraudRecommendation: "Elevated risk. Multiple delinquencies. Careful assessment required."
            )
        };
    }

    private static MockBusinessData GenerateFallbackBusinessData(string rcNumber)
    {
        var seed = rcNumber.Sum(c => c);
        var tier = seed % 2; // 0=good, 1=watch
        var businessIndex = seed % FallbackBusinessNames.Length;

        return tier == 0
            ? new MockBusinessData(
                Name: FallbackBusinessNames[businessIndex], Phone: "23418001234567",
                DateOfRegistration: "2008-04-15", Address: "12 Broad Street, Lagos Island, Lagos",
                BusinessType: "Limited Liability Company",
                TotalLoans: 15, ActiveLoans: 4, ClosedLoans: 11, PerformingLoans: 14,
                DelinquentFacilities: 1, TotalBorrowed: 380000000, TotalOutstanding: 45000000, TotalOverdue: 2500000,
                HighestLoanAmount: 80000000,
                FraudRiskScore: 30, FraudRecommendation: "Low-medium risk. Established business with good track record."
            )
            : new MockBusinessData(
                Name: FallbackBusinessNames[businessIndex], Phone: "23418009876543",
                DateOfRegistration: "2015-11-20", Address: "Suite 4, Commerce House, Kano",
                BusinessType: "Limited Liability Company",
                TotalLoans: 7, ActiveLoans: 3, ClosedLoans: 4, PerformingLoans: 4,
                DelinquentFacilities: 3, TotalBorrowed: 95000000, TotalOutstanding: 62000000, TotalOverdue: 28000000,
                HighestLoanAmount: 35000000,
                FraudRiskScore: 58, FraudRecommendation: "Medium risk. Significant overdue exposure. Recommend collateral review."
            );
    }

    private static readonly string[] FallbackMaleNames =
    [
        "ADEWALE OGUNDIMU", "EMEKA NWOSU", "IBRAHIM USMAN", "KELECHI EZE", "SUNDAY OKAFOR",
        "CHUKWUEMEKA ADEOLA", "BABATUNDE LAWAL", "MUSA ABDULLAHI", "OKECHUKWU NWANKWO", "FESTUS OGUNDELE",
        "HENRY OZOEMENA", "TUNDE ADESANYA", "YUSUF MOHAMMED", "NNAMDI OKONKWO", "ROTIMI AFOLABI"
    ];

    private static readonly string[] FallbackBusinessNames =
    [
        "GREENFIELD LOGISTICS LIMITED", "APEX TRADING COMPANY LIMITED", "HERITAGE RESOURCES NIGERIA LIMITED",
        "PIONEER AGRO INDUSTRIES LIMITED", "GOLDEN EAGLE ENTERPRISES LIMITED", "MERIDIAN PROPERTIES LIMITED",
        "NEXGEN TECHNOLOGY SOLUTIONS LIMITED", "ATLANTIC IMPORTS AND EXPORTS LIMITED",
        "CONTINENTAL MANUFACTURING LIMITED", "SILVERLINE HOLDINGS LIMITED"
    ];

    private static int CalculateCreditScore(MockIndividualData data)
    {
        var baseScore = 600;
        
        // Delinquency impact
        baseScore -= data.DelinquentFacilities * 30;
        baseScore -= Math.Min(data.MaxDelinquencyDays / 10, 50);
        
        // Positive factors
        if (data.ClosedLoans > 5) baseScore += 30;
        if (data.PerformingLoans == data.TotalLoans) baseScore += 50;
        if (data.TotalOverdue == 0) baseScore += 40;
        
        return Math.Clamp(baseScore, 300, 850);
    }

    private static List<SmartComplyCreditEnquiry> GenerateMockEnquiries(List<string> creditors)
    {
        var enquiries = new List<SmartComplyCreditEnquiry>();
        var random = Random.Shared;
        
        foreach (var creditor in creditors.Take(3))
        {
            enquiries.Add(new SmartComplyCreditEnquiry(
                LoanProvider: creditor,
                Reason: "General credit inquiry",
                Date: DateTime.UtcNow.AddMonths(-random.Next(1, 12)),
                ContactPhone: "+234800" + random.Next(1000000, 9999999)
            ));
        }
        
        return enquiries;
    }

    private static List<SmartComplyLoanPerformance> GenerateMockLoanPerformance(MockIndividualData data)
    {
        var performances = new List<SmartComplyLoanPerformance>();
        var random = Random.Shared;
        
        for (int i = 0; i < Math.Min(data.ActiveLoans, 3); i++)
        {
            var isPerforming = i < data.PerformingLoans;
            performances.Add(new SmartComplyLoanPerformance(
                LoanProvider: data.Creditors[i % data.Creditors.Count],
                AccountNumber: $"ACC{random.Next(100000, 999999)}",
                LoanAmount: random.Next(100000, 5000000),
                OutstandingBalance: isPerforming ? random.Next(0, 500000) : random.Next(500000, 2000000),
                Status: isPerforming ? "Open" : "Open",
                PerformanceStatus: isPerforming ? "Performing" : "Non-Performing",
                OverdueAmount: isPerforming ? 0 : random.Next(50000, 500000),
                Type: "Term Loan",
                LoanDuration: $"{random.Next(6, 36)}",
                RepaymentFrequency: "Monthly",
                RepaymentBehavior: isPerforming ? "None" : "Delinquent (over 30 days)",
                PaymentProfile: isPerforming ? "000000000000" : "001122334455",
                DateAccountOpened: DateTime.UtcNow.AddYears(-random.Next(1, 3)),
                LastUpdatedAt: DateTime.UtcNow.AddDays(-random.Next(1, 30))
            ));
        }
        
        return performances;
    }

    private static List<SmartComplyLoanHistory> GenerateMockLoanHistory(MockIndividualData data)
    {
        var history = new List<SmartComplyLoanHistory>();
        var random = Random.Shared;
        
        for (int i = 0; i < Math.Min(data.ClosedLoans, 5); i++)
        {
            history.Add(new SmartComplyLoanHistory(
                LoanProvider: data.Creditors[i % data.Creditors.Count],
                LoanProviderAddress: "Lagos, Nigeria",
                AccountNumber: $"ACC{random.Next(100000, 999999)}",
                Type: "Term Loan",
                LoanAmount: random.Next(100000, 2000000),
                InstallmentAmount: random.Next(10000, 100000),
                OverdueAmount: 0,
                LastPaymentDate: DateTime.UtcNow.AddMonths(-random.Next(1, 6)),
                LoanDuration: $"{random.Next(6, 24)}",
                DisbursedDate: DateTime.UtcNow.AddYears(-random.Next(2, 5)),
                MaturityDate: DateTime.UtcNow.AddMonths(-random.Next(1, 12)),
                PerformanceStatus: "Paid Off (Closed)",
                Status: "Closed",
                OutstandingBalance: 0,
                Collateral: "",
                CollateralValue: 0,
                PaymentHistory: "000000000000",
                LastUpdatedAt: DateTime.UtcNow.AddMonths(-random.Next(1, 12))
            ));
        }
        
        return history;
    }

    private static SmartComplyFinancialAnalysis GenerateMockFinancialAnalysis(decimal annualIncome, decimal loanAmount, decimal collateralValue)
    {
        var monthlyIncome = annualIncome > 0 ? annualIncome / 12 : 0;
        var dti = annualIncome > 0 ? loanAmount / annualIncome * 100 : 0;
        var ltc = collateralValue > 0 ? (loanAmount / collateralValue * 100) : 0;

        return new SmartComplyFinancialAnalysis(
            IncomeStability: new SmartComplyRiskItem(
                RiskScore: dti > 50 ? 80 : dti > 30 ? 50 : 30,
                Observation: $"Monthly income of {monthlyIncome:N0}. Debt-to-income ratio: {dti:N1}%"
            ),
            RepaymentDuration: new SmartComplyRiskItem(
                RiskScore: 50,
                Observation: "Standard repayment duration within acceptable range."
            ),
            CollateralCoverage: new SmartComplyRiskItem(
                RiskScore: collateralValue >= loanAmount ? 20 : collateralValue > 0 ? 50 : 80,
                Observation: collateralValue > 0 
                    ? $"Loan-to-collateral ratio: {ltc:N1}%"
                    : "No collateral provided. Loan is unsecured."
            ),
            DebtServiceability: new SmartComplyRiskItem(
                RiskScore: dti > 40 ? 70 : dti > 25 ? 40 : 20,
                Observation: $"Debt service coverage assessment based on {dti:N1}% DTI ratio."
            ),
            DebtToIncomeRatio: new SmartComplyRiskItem(
                RiskScore: dti > 50 ? 90 : dti > 40 ? 60 : dti > 25 ? 40 : 20,
                Observation: $"Debt-to-income ratio: {dti:N1}%"
            )
        );
    }

    private static string GenerateBusinessEmail(string businessName)
    {
        // Remove common business suffixes and generate clean domain
        var cleanName = businessName.ToLower()
            .Replace(" limited", "")
            .Replace(" ltd", "")
            .Replace(" plc", "")
            .Replace(" inc", "")
            .Replace(" ", "")
            .Replace(".", "")
            .Replace(",", "");
        
        return $"{cleanName}.com";
    }

    #endregion

    #region Mock Data Records

    private record MockIndividualData(
        string Name,
        string Phone,
        string Gender,
        string DateOfBirth,
        int TotalLoans,
        int ActiveLoans,
        int ClosedLoans,
        int PerformingLoans,
        int DelinquentFacilities,
        decimal TotalBorrowed,
        decimal TotalOutstanding,
        decimal TotalOverdue,
        decimal HighestLoanAmount,
        int MaxDelinquencyDays,
        List<string> Creditors,
        int FraudRiskScore,
        string FraudRecommendation
    );

    private record MockBusinessData(
        string Name,
        string Phone,
        string DateOfRegistration,
        string Address,
        string BusinessType,
        int TotalLoans,
        int ActiveLoans,
        int ClosedLoans,
        int PerformingLoans,
        int DelinquentFacilities,
        decimal TotalBorrowed,
        decimal TotalOutstanding,
        decimal TotalOverdue,
        decimal HighestLoanAmount,
        int FraudRiskScore,
        string FraudRecommendation
    );

    #endregion
}
