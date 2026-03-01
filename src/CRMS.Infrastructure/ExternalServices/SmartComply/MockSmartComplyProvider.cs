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
            return Task.FromResult(Result.Failure<SmartComplyIndividualCreditReport>("Subject not found in credit bureau"));
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
            return Task.FromResult(Result.Failure<SmartComplyCreditScore>("Subject not found"));
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
            return Task.FromResult(Result.Failure<SmartComplyBusinessCreditReport>("Business not found in credit bureau"));
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
            return Task.FromResult(Result.Failure<SmartComplyBvnResult>("BVN not found"));
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
            return Task.FromResult(Result.Failure<SmartComplyBvnAdvancedResult>("BVN not found"));
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

    public Task<Result<SmartComplyCacResult>> VerifyCacAdvancedAsync(string rcNumber, CancellationToken ct = default)
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
            Directors: [
                new SmartComplyCacDirector("John Adebayo", "Managing Director", "2010-01-01"),
                new SmartComplyCacDirector("Amina Ibrahim", "Executive Director", "2012-05-15"),
                new SmartComplyCacDirector("Chukwuma Okonkwo", "Non-Executive Director", "2015-08-20")
            ]
        )));
    }

    #endregion

    #region Helper Methods

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
