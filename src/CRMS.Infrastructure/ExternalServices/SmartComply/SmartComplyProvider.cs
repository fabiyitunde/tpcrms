using System.Net.Http.Json;
using System.Text.Json;
using CRMS.Domain.Common;
using CRMS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.ExternalServices.SmartComply;

public class SmartComplyProvider : ISmartComplyProvider
{
    private readonly HttpClient _httpClient;
    private readonly SmartComplySettings _settings;
    private readonly ILogger<SmartComplyProvider> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SmartComplyProvider(
        HttpClient httpClient,
        IOptions<SmartComplySettings> settings,
        ILogger<SmartComplyProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("x-access-token", _settings.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new FlexibleDecimalConverter(),
                new FlexibleNullableDecimalConverter(),
                new FlexibleIntConverter(),
                new FlexibleDateTimeConverter(),
                new FlexibleNullableDateTimeConverter(),
                new FlexibleStringConverter()
            }
        };
    }

    #region Individual Credit Reports

    public async Task<Result<SmartComplyIndividualCreditReport>> GetFirstCentralSummaryAsync(string bvn, CancellationToken ct = default)
    {
        return await GetIndividualCreditReportAsync(SmartComplyEndpoints.Individual.FirstCentralSummary, bvn, "FirstCentralSummary", ct);
    }

    public async Task<Result<SmartComplyIndividualCreditReport>> GetFirstCentralFullAsync(string bvn, CancellationToken ct = default)
    {
        return await GetIndividualCreditReportAsync(SmartComplyEndpoints.Individual.FirstCentralFull, bvn, "FirstCentralFull", ct);
    }

    public async Task<Result<SmartComplyCreditScore>> GetFirstCentralScoreAsync(string bvn, CancellationToken ct = default)
    {
        return await GetCreditScoreAsync(SmartComplyEndpoints.Individual.FirstCentralScore, bvn, "FirstCentral", ct);
    }

    public async Task<Result<SmartComplyIndividualCreditReport>> GetCreditRegistrySummaryAsync(string bvn, CancellationToken ct = default)
    {
        return await GetIndividualCreditReportAsync(SmartComplyEndpoints.Individual.CreditRegistrySummary, bvn, "CreditRegistrySummary", ct);
    }

    public async Task<Result<SmartComplyIndividualCreditReport>> GetCreditRegistryFullAsync(string bvn, CancellationToken ct = default)
    {
        return await GetIndividualCreditReportAsync(SmartComplyEndpoints.Individual.CreditRegistryFull, bvn, "CreditRegistryFull", ct);
    }

    public async Task<Result<SmartComplyIndividualCreditReport>> GetCreditRegistryAdvancedAsync(string bvn, CancellationToken ct = default)
    {
        return await GetIndividualCreditReportAsync(SmartComplyEndpoints.Individual.CreditRegistryAdvanced, bvn, "CreditRegistryAdvanced", ct);
    }

    public async Task<Result<SmartComplyCreditScore>> GetCRCScoreAsync(string bvn, CancellationToken ct = default)
    {
        return await GetCreditScoreAsync(SmartComplyEndpoints.Individual.CRCScore, bvn, "CRC", ct);
    }

    public async Task<Result<SmartComplyIndividualCreditReport>> GetCRCHistoryAsync(string bvn, CancellationToken ct = default)
    {
        return await GetIndividualCreditReportAsync(SmartComplyEndpoints.Individual.CRCHistory, bvn, "CRCHistory", ct);
    }

    public async Task<Result<SmartComplyIndividualCreditReport>> GetCRCFullAsync(string bvn, CancellationToken ct = default)
    {
        return await GetIndividualCreditReportAsync(SmartComplyEndpoints.Individual.CRCFull, bvn, "CRCFull", ct);
    }

    public async Task<Result<SmartComplyIndividualCreditReport>> GetCreditPremiumAsync(string bvn, CancellationToken ct = default)
    {
        return await GetIndividualCreditReportAsync(SmartComplyEndpoints.Individual.CreditPremium, bvn, "CreditPremium", ct);
    }

    private async Task<Result<SmartComplyIndividualCreditReport>> GetIndividualCreditReportAsync(
        string endpoint, string bvn, string reportType, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Requesting {ReportType} for BVN {BvnMasked}", reportType, MaskBvn(bvn));

            var request = new { bvn };
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("{ReportType} request failed: {StatusCode} - {Error}", 
                    reportType, response.StatusCode, errorContent);
                
                // Normalize to "not found" for consistent detection in the handler.
                // SmartComply uses 404 for identity not found, but 400 for "No credit data available"
                // — both mean the same thing: no bureau record exists for this person.
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    errorContent.Contains("No credit data available", StringComparison.OrdinalIgnoreCase))
                    return Result.Failure<SmartComplyIndividualCreditReport>("Subject not found in credit bureau");

                return Result.Failure<SmartComplyIndividualCreditReport>(FormatApiError(response.StatusCode, errorContent));
            }

            var result = await response.Content.ReadFromJsonAsync<SmartComplyResponse<IndividualCreditReportData>>(_jsonOptions, ct);

            if (result == null || result.Data == null)
            {
                return Result.Failure<SmartComplyIndividualCreditReport>(result?.Message ?? "No data returned");
            }

            // SmartComply sometimes omits the success boolean and uses status string instead
            if (!result.IsSuccessful)
            {
                _logger.LogWarning("{ReportType} response has non-success status but data is present; status: {Status}, message: {Message}",
                    reportType, result.Status, result.Message);
            }

            return Result.Success(MapToIndividualCreditReport(result.Data));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during {ReportType} request", reportType);
            return Result.Failure<SmartComplyIndividualCreditReport>($"Connection error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // True HTTP timeout (not app cancellation)
            _logger.LogError(ex, "Timeout during {ReportType} request", reportType);
            return Result.Failure<SmartComplyIndividualCreditReport>("Request timed out");
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate real cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during {ReportType} request", reportType);
            return Result.Failure<SmartComplyIndividualCreditReport>($"Unexpected error: {ex.Message}");
        }
    }

    private async Task<Result<SmartComplyCreditScore>> GetCreditScoreAsync(
        string endpoint, string bvn, string provider, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Requesting {Provider} credit score for BVN {BvnMasked}", provider, MaskBvn(bvn));

            var request = new { bvn };
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                return Result.Failure<SmartComplyCreditScore>(FormatApiError(response.StatusCode, errorContent));
            }

            // Read raw body first so we can log it on failure and avoid losing the stream
            var rawBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogDebug("{Provider} credit_scores_crc raw response: {Body}",
                provider, rawBody.Length > 3000 ? rawBody[..3000] : rawBody);

            SmartComplyResponse<CreditScoreOnlyData>? result;
            try
            {
                result = System.Text.Json.JsonSerializer.Deserialize<SmartComplyResponse<CreditScoreOnlyData>>(rawBody, _jsonOptions);
            }
            catch (JsonException jex)
            {
                _logger.LogError(jex, "{Provider} credit_scores_crc deserialization failed. Raw body: {Body}",
                    provider, rawBody.Length > 3000 ? rawBody[..3000] : rawBody);
                return Result.Failure<SmartComplyCreditScore>($"Score response parse error: {jex.Message}");
            }

            if (result == null || result.Data?.Score == null)
            {
                _logger.LogWarning("{Provider} credit_scores_crc returned null data or null score. success={Success}, status={Status}, message={Message}",
                    provider, result?.Success, result?.Status, result?.Message);
                return Result.Failure<SmartComplyCreditScore>(result?.Message ?? "No score data returned");
            }

            var scoreDetail = result.Data.Score;

            // credit_scores_crc returns score under data.score.ficoScore.score (int) and ficoScore.rating (string)
            if (scoreDetail.FicoScore is { Score: > 0 } fico)
            {
                _logger.LogInformation("{Provider} FICO score found: {Score} ({Rating})", provider, fico.Score, fico.Rating);
                return Result.Success(new SmartComplyCreditScore(
                    fico.Score,
                    fico.Rating ?? "UNKNOWN",
                    provider,
                    result.Data.SearchedDate ?? DateTime.UtcNow
                ));
            }

            // Fall back to totalConsumerScore string (used by some other score endpoints)
            var parsedScore = scoreDetail.ParsedScore;
            _logger.LogInformation("{Provider} score — ficoScore={FicoPresent}, ficoScore.score={FicoScore}, totalConsumerScore={TotalConsumerScore}",
                provider,
                scoreDetail.FicoScore != null,
                scoreDetail.FicoScore?.Score,
                scoreDetail.TotalConsumerScore);

            return Result.Success(new SmartComplyCreditScore(
                parsedScore ?? 0,
                string.IsNullOrWhiteSpace(scoreDetail.Description) && parsedScore == null
                    ? "NO SCORE"
                    : scoreDetail.Description ?? "NO SCORE",
                provider,
                result.Data.SearchedDate ?? DateTime.UtcNow
            ));
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {Provider} credit score request", provider);
            return Result.Failure<SmartComplyCreditScore>($"Error: {ex.Message}");
        }
    }

    #endregion

    #region Business Credit Reports

    public async Task<Result<SmartComplyBusinessCreditReport>> GetCRCBusinessHistoryAsync(string rcNumber, CancellationToken ct = default)
    {
        return await GetBusinessCreditReportAsync(SmartComplyEndpoints.Business.CRCHistory, rcNumber, "CRCBusinessHistory", ct);
    }

    public async Task<Result<SmartComplyBusinessCreditReport>> GetFirstCentralBusinessAsync(string rcNumber, CancellationToken ct = default)
    {
        return await GetBusinessCreditReportAsync(SmartComplyEndpoints.Business.FirstCentral, rcNumber, "FirstCentralBusiness", ct);
    }

    public async Task<Result<SmartComplyBusinessCreditReport>> GetPremiumBusinessAsync(string rcNumber, CancellationToken ct = default)
    {
        return await GetBusinessCreditReportAsync(SmartComplyEndpoints.Business.Premium, rcNumber, "PremiumBusiness", ct);
    }

    private async Task<Result<SmartComplyBusinessCreditReport>> GetBusinessCreditReportAsync(
        string endpoint, string rcNumber, string reportType, CancellationToken ct)
    {
        try
        {
            // SmartComply requires the "RC" prefix (e.g. "RC1275857"). Normalise silently so
            // bare numeric strings stored in the DB still work.
            var normalizedRc = rcNumber.StartsWith("RC", StringComparison.OrdinalIgnoreCase)
                ? rcNumber
                : "RC" + rcNumber;

            _logger.LogInformation("Requesting {ReportType} for RC {RcNumber}", reportType, normalizedRc);

            var request = new { registration_number = normalizedRc };
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("{ReportType} request failed: {StatusCode} - {Error}", 
                    reportType, response.StatusCode, errorContent);
                
                // Normalize to "not found" for consistent detection in the handler.
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    errorContent.Contains("No credit data available", StringComparison.OrdinalIgnoreCase))
                    return Result.Failure<SmartComplyBusinessCreditReport>("Business not found in credit bureau");

                return Result.Failure<SmartComplyBusinessCreditReport>(FormatApiError(response.StatusCode, errorContent));
            }

            var result = await response.Content.ReadFromJsonAsync<SmartComplyResponse<BusinessCreditReportData>>(_jsonOptions, ct);
            
            if (result == null || result.Data == null)
            {
                return Result.Failure<SmartComplyBusinessCreditReport>(result?.Message ?? "No data returned");
            }

            return Result.Success(MapToBusinessCreditReport(result.Data));
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during {ReportType} request", reportType);
            return Result.Failure<SmartComplyBusinessCreditReport>($"Error: {ex.Message}");
        }
    }

    #endregion

    #region Loan Fraud Check

    public async Task<Result<SmartComplyLoanFraudResult>> CheckIndividualLoanFraudAsync(
        SmartComplyIndividualLoanRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Checking individual loan fraud for {Name}", $"{request.FirstName} {request.LastName}");

            var payload = new IndividualLoanFraudRequest
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                OtherName = request.OtherName,
                DateOfBirth = request.DateOfBirth,
                Gender = request.Gender,
                Country = request.Country,
                City = request.City,
                CurrentAddress = request.CurrentAddress,
                Bvn = request.Bvn,
                PhoneNumber = request.PhoneNumber,
                EmailAddress = request.EmailAddress,
                EmploymentType = request.EmploymentType,
                JobRole = request.JobRole,
                EmployerName = request.EmployerName,
                AnnualIncome = request.AnnualIncome,
                BankName = request.BankName,
                AccountNumber = request.AccountNumber,
                LoanAmountRequested = request.LoanAmountRequested,
                PurposeOfLoan = request.PurposeOfLoan,
                LoanRepaymentDurationType = request.LoanRepaymentDurationType,
                LoanRepaymentDurationValue = request.LoanRepaymentDurationValue,
                CollateralRequired = request.CollateralRequired,
                CollateralValue = request.CollateralValue,
                IsIndividual = true,
                RunAmlCheck = request.RunAmlCheck
            };

            var response = await _httpClient.PostAsJsonAsync(SmartComplyEndpoints.LoanFraud.FraudCheck, payload, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return Result.Failure<SmartComplyLoanFraudResult>("Applicant not found for fraud check");
                    
                return Result.Failure<SmartComplyLoanFraudResult>(FormatApiError(response.StatusCode, errorContent));
            }

            var result = await response.Content.ReadFromJsonAsync<SmartComplyResponse<LoanFraudCheckData>>(_jsonOptions, ct);
            
            if (result == null || result.Data == null)
            {
                return Result.Failure<SmartComplyLoanFraudResult>(result?.Message ?? "No data returned");
            }

            return Result.Success(MapToLoanFraudResult(result.Data));
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during individual loan fraud check");
            return Result.Failure<SmartComplyLoanFraudResult>($"Error: {ex.Message}");
        }
    }

    public async Task<Result<SmartComplyLoanFraudResult>> CheckBusinessLoanFraudAsync(
        SmartComplyBusinessLoanRequest request, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Checking business loan fraud for {Name}", request.BusinessName);

            var payload = new BusinessLoanFraudRequest
            {
                BusinessName = request.BusinessName,
                BusinessAddress = request.BusinessAddress,
                RcNumber = request.RcNumber,
                City = request.City,
                Country = request.Country,
                PhoneNumber = request.PhoneNumber,
                EmailAddress = request.EmailAddress,
                AnnualRevenue = request.AnnualRevenue,
                BankName = request.BankName,
                AccountNumber = request.AccountNumber,
                LoanAmountRequested = request.LoanAmountRequested,
                PurposeOfLoan = request.PurposeOfLoan,
                LoanRepaymentDurationType = request.LoanRepaymentDurationType,
                LoanRepaymentDurationValue = request.LoanRepaymentDurationValue,
                CollateralRequired = request.CollateralRequired,
                CollateralValue = request.CollateralValue,
                IsBusiness = true,
                RunAmlCheck = request.RunAmlCheck
            };

            var response = await _httpClient.PostAsJsonAsync(SmartComplyEndpoints.LoanFraud.FraudCheck, payload, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return Result.Failure<SmartComplyLoanFraudResult>("Business not found for fraud check");
                    
                return Result.Failure<SmartComplyLoanFraudResult>(FormatApiError(response.StatusCode, errorContent));
            }

            var result = await response.Content.ReadFromJsonAsync<SmartComplyResponse<LoanFraudCheckData>>(_jsonOptions, ct);
            
            if (result == null || result.Data == null)
            {
                return Result.Failure<SmartComplyLoanFraudResult>(result?.Message ?? "No data returned");
            }

            return Result.Success(MapToLoanFraudResult(result.Data));
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during business loan fraud check");
            return Result.Failure<SmartComplyLoanFraudResult>($"Error: {ex.Message}");
        }
    }

    #endregion

    #region KYC/Identity Verification

    public async Task<Result<SmartComplyBvnResult>> VerifyBvnAsync(string bvn, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Verifying BVN {BvnMasked}", MaskBvn(bvn));

            var request = new BvnVerificationRequest { Bvn = bvn };
            var response = await _httpClient.PostAsJsonAsync(SmartComplyEndpoints.KycNigeria.BVN, request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                return Result.Failure<SmartComplyBvnResult>(FormatApiError(response.StatusCode, errorContent));
            }

            var result = await response.Content.ReadFromJsonAsync<SmartComplyResponse<BvnVerificationData>>(_jsonOptions, ct);
            
            if (result == null || result.Data == null)
            {
                return Result.Failure<SmartComplyBvnResult>(result?.Message ?? "Verification failed");
            }

            return Result.Success(new SmartComplyBvnResult(
                result.Data.FirstName,
                result.Data.MiddleName,
                result.Data.LastName,
                result.Data.DateOfBirth,
                result.Data.PhoneNumber1
            ));
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during BVN verification");
            return Result.Failure<SmartComplyBvnResult>($"Error: {ex.Message}");
        }
    }

    public async Task<Result<SmartComplyBvnAdvancedResult>> VerifyBvnAdvancedAsync(string bvn, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Verifying BVN Advanced {BvnMasked}", MaskBvn(bvn));

            var request = new BvnVerificationRequest { Bvn = bvn };
            var response = await _httpClient.PostAsJsonAsync(SmartComplyEndpoints.KycNigeria.BVNAdvanced, request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                return Result.Failure<SmartComplyBvnAdvancedResult>(FormatApiError(response.StatusCode, errorContent));
            }

            var result = await response.Content.ReadFromJsonAsync<SmartComplyResponse<BvnAdvancedData>>(_jsonOptions, ct);
            
            if (result == null || result.Data == null)
            {
                return Result.Failure<SmartComplyBvnAdvancedResult>(result?.Message ?? "Verification failed");
            }

            var data = result.Data;
            return Result.Success(new SmartComplyBvnAdvancedResult(
                data.Bvn, data.Image, data.Title, data.Gender,
                data.FirstName, data.MiddleName, data.LastName,
                data.DateOfBirth, data.PhoneNumber1, data.PhoneNumber2,
                data.MaritalStatus, data.StateOfOrigin, data.LgaOfOrigin,
                data.StateOfResidence, data.LgaOfResidence, data.ResidentialAddress,
                data.EnrollmentBank, data.EnrollmentBranch, data.RegistrationDate,
                data.LevelOfAccount, data.WatchListed
            ));
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during BVN Advanced verification");
            return Result.Failure<SmartComplyBvnAdvancedResult>($"Error: {ex.Message}");
        }
    }

    public async Task<Result<SmartComplyNinResult>> VerifyNinAsync(string nin, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Verifying NIN");

            var request = new NinVerificationRequest { Nin = nin };
            var response = await _httpClient.PostAsJsonAsync(SmartComplyEndpoints.KycNigeria.NIN, request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                return Result.Failure<SmartComplyNinResult>(FormatApiError(response.StatusCode, errorContent));
            }

            var result = await response.Content.ReadFromJsonAsync<SmartComplyResponse<NinVerificationData>>(_jsonOptions, ct);
            
            if (result == null || result.Data == null)
            {
                return Result.Failure<SmartComplyNinResult>(result?.Message ?? "Verification failed");
            }

            var data = result.Data;
            return Result.Success(new SmartComplyNinResult(
                data.Nin, data.FirstName, data.MiddleName, data.LastName,
                data.DateOfBirth, data.Gender, data.TelephoneNumber, data.Photo
            ));
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during NIN verification");
            return Result.Failure<SmartComplyNinResult>($"Error: {ex.Message}");
        }
    }

    public async Task<Result<SmartComplyTinResult>> VerifyTinAsync(string tin, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Verifying TIN");

            var request = new TinVerificationRequest { TaxIdentificationNumber = tin };
            var response = await _httpClient.PostAsJsonAsync(SmartComplyEndpoints.KycNigeria.TIN, request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                return Result.Failure<SmartComplyTinResult>(FormatApiError(response.StatusCode, errorContent));
            }

            var result = await response.Content.ReadFromJsonAsync<SmartComplyResponse<TinVerificationData>>(_jsonOptions, ct);
            
            if (result == null || result.Data == null)
            {
                return Result.Failure<SmartComplyTinResult>(result?.Message ?? "Verification failed");
            }

            var data = result.Data;
            return Result.Success(new SmartComplyTinResult(
                data.Search, data.TaxpayerName, data.CacRegNumber,
                data.TaxOffice, data.PhoneNumber, data.Email
            ));
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TIN verification");
            return Result.Failure<SmartComplyTinResult>($"Error: {ex.Message}");
        }
    }

    public async Task<Result<SmartComplyCacResult>> VerifyCacAsync(string rcNumber, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Verifying CAC (basic) for RC {RcNumber}", rcNumber);

            var request = new CacVerificationRequest { RcNumber = rcNumber };
            var response = await _httpClient.PostAsJsonAsync(SmartComplyEndpoints.KycNigeria.CAC, request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                return Result.Failure<SmartComplyCacResult>(FormatApiError(response.StatusCode, errorContent));
            }

            var result = await response.Content.ReadFromJsonAsync<SmartComplyResponse<CacVerificationData>>(_jsonOptions, ct);

            if (result == null || result.Data == null)
                return Result.Failure<SmartComplyCacResult>(result?.Message ?? "Verification failed");

            var d = result.Data;
            return Result.Success(new SmartComplyCacResult(
                CompanyName: d.CompanyName,
                RcNumber: d.RcNumber,
                CompanyType: d.CompanyType,
                RegistrationDate: d.RegistrationDate,
                Address: d.Address,
                City: d.City,
                State: d.State,
                Email: d.Email,
                Status: d.Status,
                NatureOfBusiness: d.NatureOfBusiness,
                ShareCapital: d.ShareCapital,
                CompanyId: null,
                Directors: d.Directors?.Select(dir => new SmartComplyCacDirector(
                    Id: null,
                    Surname: null,
                    FirstName: null,
                    OtherName: null,
                    FullName: dir.Name,
                    Gender: null,
                    DateOfBirth: null,
                    Nationality: null,
                    Occupation: null,
                    Email: null,
                    PhoneNumber: null,
                    Address: null,
                    City: null,
                    State: null,
                    Lga: null,
                    Status: null,
                    IsChairman: null,
                    IsCorporate: null,
                    DateOfAppointment: dir.DateOfAppointment,
                    AffiliateType: dir.Designation,
                    TypeOfShares: null,
                    NumSharesAlloted: null,
                    IdentityNumber: null,
                    Country: null
                )).ToList() ?? []
            ));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CAC (basic) verification");
            return Result.Failure<SmartComplyCacResult>($"Error: {ex.Message}");
        }
    }

    public async Task<Result<SmartComplyCacResult>> VerifyCacAdvancedAsync(string rcNumber, string companyName, string companyType = "RC", CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Verifying CAC (advanced) for RC {RcNumber}", rcNumber);

            var request = new CacAdvancedVerificationRequest
            {
                RegistrationNumber = rcNumber,
                CompanyName = companyName,
                CompanyType = companyType
            };
            var response = await _httpClient.PostAsJsonAsync(SmartComplyEndpoints.KycNigeria.CACAdvanced, request, ct);

            var rawJson = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return Result.Failure<SmartComplyCacResult>(FormatApiError(response.StatusCode, rawJson));

            var result = System.Text.Json.JsonSerializer.Deserialize<SmartComplyResponse<CacAdvancedData>>(rawJson, _jsonOptions);

            if (result == null || result.Data == null)
                return Result.Failure<SmartComplyCacResult>(result?.Message ?? "Verification failed");

            // Advanced endpoint returns status:"success" at top level (not a bool Success field)
            if (!string.IsNullOrEmpty(result.Status) && result.Status.ToLower() != "success" && !result.Success)
                return Result.Failure<SmartComplyCacResult>(result.Message ?? "Verification failed");

            var d = result.Data;
            return Result.Success(MapCacAdvancedToResult(d));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CAC (advanced) verification");
            return Result.Failure<SmartComplyCacResult>($"Error: {ex.Message}");
        }
    }

    private static SmartComplyCacResult MapCacAdvancedToResult(CacAdvancedData d)
    {
        // The CAC Advanced `directors` array contains all affiliate types (DIRECTOR, SHAREHOLDER,
        // PSC, PRESENTER, WITNESS, etc.) and duplicates the same person across multiple rows.
        // Filter to active directors only — the only set we need for KYC on a loan application.
        var directors = (d.Directors ?? [])
            .Where(r =>
                string.Equals(r.AffiliateTypeFk?.Name, "DIRECTOR", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            .Select(MapCacAdvancedDirector)
            .ToList();

        return new SmartComplyCacResult(
            CompanyName: d.CompanyName,
            RcNumber: d.RcNumber,
            CompanyType: d.EntityType,
            RegistrationDate: d.RegistrationDate,
            Address: d.CompanyAddress,
            City: d.City,
            State: d.State,
            Email: d.EmailAddress,
            Status: d.CompanyStatus,
            NatureOfBusiness: null,
            ShareCapital: null,
            CompanyId: d.CompanyId,
            Directors: directors
        );
    }

    private static SmartComplyCacDirector MapCacAdvancedDirector(CacAdvancedDirectorData dir)
    {
        var parts = new[] { dir.Firstname, dir.OtherName, dir.Surname }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var fullName = string.Join(" ", parts);

        return new SmartComplyCacDirector(
            Id: dir.Id,
            Surname: dir.Surname,
            FirstName: dir.Firstname,
            OtherName: string.IsNullOrWhiteSpace(dir.OtherName) ? null : dir.OtherName,
            FullName: string.IsNullOrWhiteSpace(fullName) ? null : fullName,
            Gender: dir.Gender,
            DateOfBirth: dir.DateOfBirth,
            Nationality: dir.Nationality,
            Occupation: dir.Occupation,
            Email: dir.Email,
            PhoneNumber: dir.PhoneNumber,
            Address: dir.Address,
            City: dir.City,
            State: dir.State,
            Lga: dir.Lga,
            Status: dir.Status,
            IsChairman: dir.IsChairman,
            IsCorporate: dir.IsCorporate,
            DateOfAppointment: dir.DateOfAppointment,
            AffiliateType: dir.AffiliateTypeFk?.Name,
            TypeOfShares: string.IsNullOrWhiteSpace(dir.TypeOfShares) ? null : dir.TypeOfShares,
            NumSharesAlloted: dir.NumSharesAlloted,
            IdentityNumber: string.IsNullOrWhiteSpace(dir.IdentityNumber) ? null : dir.IdentityNumber,
            Country: dir.CountryFk?.Name
        );
    }

    #endregion

    #region Mapping Helpers

    private static SmartComplyIndividualCreditReport MapToIndividualCreditReport(IndividualCreditReportData data)
    {
        var score = data.Score;
        
        return new SmartComplyIndividualCreditReport(
            data.Id,
            data.Bvn,
            data.Name,
            data.Phone,
            data.Gender,
            data.DateOfBirth,
            data.Address,
            data.Email,
            new SmartComplyCreditSummary(
                score?.TotalNoOfLoans ?? 0,
                score?.TotalNoOfInstitutions ?? 0,
                score?.TotalNoOfActiveLoans ?? 0,
                score?.TotalNoOfClosedLoans ?? 0,
                score?.TotalNoOfPerformingLoans ?? 0,
                score?.TotalNoOfDelinquentFacilities ?? 0,
                score?.HighestLoanAmount ?? 0,
                score?.TotalMonthlyInstallment ?? 0,
                score?.TotalBorrowed ?? 0,
                score?.TotalOutstanding ?? 0,
                score?.TotalOverdue ?? 0,
                score?.MaxNoOfDays ?? 0,
                score?.CrcReportOrderNumber
            ),
            score?.Creditors?.Select(c => new SmartComplyCreditor(c.SubscriberId, c.Name, c.Phone, c.Address)).ToList() ?? [],
            score?.CreditEnquiries?.Select(e => new SmartComplyCreditEnquiry(e.LoanProvider, e.Reason, e.Date, e.ContactPhone)).ToList() ?? [],
            score?.LoanPerformance?.Select(p => new SmartComplyLoanPerformance(
                p.LoanProvider, p.AccountNumber, p.LoanAmount, p.OutstandingBalance,
                p.Status, p.PerformanceStatus, p.OverdueAmount, p.Type,
                p.LoanDuration, p.RepaymentFrequency, p.RepaymentBehavior,
                p.PaymentProfile, p.DateAccountOpened, p.LastUpdatedAt
            )).ToList() ?? [],
            score?.LoanHistory?.Select(h => new SmartComplyLoanHistory(
                h.LoanProvider, h.LoanProviderAddress, h.AccountNumber, h.Type,
                h.LoanAmount, h.InstallmentAmount, h.OverdueAmount, h.LastPaymentDate,
                h.LoanDuration, h.DisbursedDate, h.MaturityDate, h.PerformanceStatus,
                h.Status, h.OutstandingBalance, h.Collateral, h.CollateralValue,
                h.PaymentHistory, h.LastUpdatedAt
            )).ToList() ?? [],
            data.SearchedDate
        );
    }

    private static SmartComplyBusinessCreditReport MapToBusinessCreditReport(BusinessCreditReportData data)
    {
        var score = data.Score;
        
        return new SmartComplyBusinessCreditReport(
            data.Id,
            data.BusinessRegNo,
            data.Name,
            data.Phone,
            data.DateOfRegistration,
            data.Address,
            data.Website,
            data.TaxIdentificationNumber,
            data.NoOfDirectors,
            data.Industry,
            data.BusinessType,
            data.Email,
            new SmartComplyBusinessCreditSummary(
                score?.TotalNoOfLoans ?? 0,
                score?.TotalNoOfActiveLoans ?? 0,
                score?.TotalNoOfClosedLoans ?? 0,
                score?.TotalNoOfInstitutions ?? 0,
                score?.TotalOverdue ?? 0,
                score?.TotalBorrowed ?? 0,
                score?.HighestLoanAmount ?? 0,
                score?.TotalOutstanding ?? 0,
                score?.TotalMonthlyInstallment ?? 0,
                score?.TotalNoOfOverdueAccounts ?? 0,
                score?.TotalNoOfPerformingLoans ?? 0,
                score?.TotalNoOfDelinquentFacilities ?? 0,
                score?.LastReportedDate,
                score?.CrcReportOrderNumber ?? score?.FirstCentralEnquiryResultID
            ),
            data.SearchedDate
        );
    }

    private static SmartComplyLoanFraudResult MapToLoanFraudResult(LoanFraudCheckData data)
    {
        var applicantName = data.IsIndividual 
            ? $"{data.FirstName} {data.LastName}".Trim()
            : data.BusinessName;
        
        var applicantId = data.IsIndividual ? data.Bvn : data.RcNumber;

        SmartComplyFinancialAnalysis? financialAnalysis = null;
        if (data.KeyFinancialAnalysis != null)
        {
            var kfa = data.KeyFinancialAnalysis;
            financialAnalysis = new SmartComplyFinancialAnalysis(
                kfa.IncomeStability != null ? new SmartComplyRiskItem(kfa.IncomeStability.RiskScore, kfa.IncomeStability.Observation) : null,
                kfa.RepaymentDuration != null ? new SmartComplyRiskItem(kfa.RepaymentDuration.RiskScore, kfa.RepaymentDuration.Observation) : null,
                kfa.CollateralCoverage != null ? new SmartComplyRiskItem(kfa.CollateralCoverage.RiskScore, kfa.CollateralCoverage.Observation) : null,
                kfa.DebtServiceability != null ? new SmartComplyRiskItem(kfa.DebtServiceability.RiskScore, kfa.DebtServiceability.Observation) : null,
                kfa.DebtToIncomeRatio != null ? new SmartComplyRiskItem(kfa.DebtToIncomeRatio.RiskScore, kfa.DebtToIncomeRatio.Observation) : null
            );
        }

        SmartComplyLoanFraudHistory? history = null;
        if (data.History != null)
        {
            var h = data.History;
            history = new SmartComplyLoanFraudHistory(
                h.TotalNoOfLoans, h.TotalNoOfInstitutions, h.TotalNoOfActiveLoans,
                h.TotalNoOfClosedLoans, h.TotalNoOfPerformingLoans, h.TotalNoOfDelinquentFacilities,
                h.HighestLoanAmount, h.TotalBorrowed, h.TotalOutstanding, h.TotalOverdue
            );
        }

        return new SmartComplyLoanFraudResult(
            data.Id,
            applicantName,
            applicantId,
            data.IsIndividual,
            data.IsBusiness,
            data.FraudRiskScore,
            data.Recommendation,
            data.Status,
            financialAnalysis,
            history,
            data.DateCreated
        );
    }

    private static string MaskBvn(string bvn)
    {
        if (string.IsNullOrEmpty(bvn) || bvn.Length < 6)
            return "****";
        return $"****{bvn[^4..]}";
    }

    private static string FormatApiError(System.Net.HttpStatusCode statusCode, string? errorContent)
    {
        return string.IsNullOrWhiteSpace(errorContent) 
            ? $"API request failed: {statusCode}"
            : $"API request failed: {statusCode} - {errorContent}";
    }

    #endregion
}
