using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRMS.Application.Advisory.Commands;
using CRMS.Application.Advisory.DTOs;
using CRMS.Application.Advisory.Queries;
using CRMS.Application.Audit.DTOs;
using CRMS.Application.Audit.Queries;
using CRMS.Application.Collateral.Commands;
using CRMS.Application.Collateral.DTOs;
using CRMS.Application.Collateral.Queries;
using CRMS.Application.Committee.Commands;
using CRMS.Application.Committee.DTOs;
using CRMS.Application.Committee.Queries;
using CRMS.Application.Common;
using CRMS.Application.FinancialAnalysis.Commands;
using CRMS.Application.FinancialAnalysis.DTOs;
using CRMS.Application.FinancialAnalysis.Queries;
using CRMS.Application.Guarantor.Commands;
using CRMS.Application.Guarantor.DTOs;
using CRMS.Application.Guarantor.Queries;
using CRMS.Application.Identity.DTOs;
using CRMS.Application.Identity.Queries;
using CRMS.Application.LoanApplication.Commands;
using CRMS.Application.LoanApplication.DTOs;
using CRMS.Application.LoanApplication.Queries;
using CRMS.Application.LoanPack.Commands;
using CRMS.Application.ProductCatalog.DTOs;
using CRMS.Application.ProductCatalog.Queries;
using CRMS.Application.Reporting.DTOs;
using CRMS.Application.Reporting.Interfaces;
using CRMS.Application.Workflow.Commands;
using CRMS.Application.Workflow.DTOs;
using CRMS.Application.Workflow.Queries;
using CRMS.Domain.Aggregates.Collateral;
using CRMS.Domain.Aggregates.FinancialStatement;
using CRMS.Domain.Aggregates.Guarantor;
using CRMS.Domain.Aggregates.LoanApplication;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.ValueObjects;
using CRMS.Web.Intranet.Components.Pages.Applications.Modals;
using CRMS.Web.Intranet.Models;
using CRMS.Application.CreditBureau.DTOs;
using CRMS.Application.CreditBureau.Queries;
using CRMS.Application.Configuration.Commands;
using CRMS.Application.Configuration.DTOs;
using CRMS.Application.Configuration.Queries;
using CRMS.Web.Intranet.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CRMS.Web.Intranet.Services;

public partial class ApplicationService
{
    private readonly IServiceProvider _sp;

    private readonly IReportingService _reporting;

    private readonly ILogger<ApplicationService> _logger;

    private readonly BankSettings _bankSettings;

    public async Task<List<BureauReportInfo>> GetBureauReportsAsync(Guid loanApplicationId)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetBureauReportsByLoanApplicationHandler>();
            var result = await handler.Handle(new GetBureauReportsByLoanApplicationQuery(loanApplicationId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<BureauReportInfo>();
            }
            return result.Data.Select(r => new BureauReportInfo
            {
                Id = r.Id,
                SubjectName = r.SubjectName,
                SubjectType = r.SubjectType,
                Provider = r.Provider,
                Status = r.Status,
                CreditScore = r.CreditScore,
                Rating = GetScoreGrade(r.CreditScore),
                ActiveLoans = r.ActiveLoans,
                TotalExposure = r.TotalOutstandingBalance,
                TotalOverdue = r.TotalOverdue,
                MaxDelinquencyDays = r.MaxDelinquencyDays,
                HasLegalIssues = r.HasLegalActions,
                ReportDate = r.CompletedAt ?? r.RequestedAt,
                FraudRiskScore = r.FraudRiskScore,
                FraudRecommendation = r.FraudRecommendation,
                PartyId = r.PartyId,
                PartyType = r.PartyType
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bureau reports for application {Id}", loanApplicationId);
            return new List<BureauReportInfo>();
        }
    }

    public async Task<ApiResponse> RequestBureauChecksAsync(Guid applicationId, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.CreditBureau.Commands.ProcessLoanCreditChecksHandler>();
            var result = await handler.Handle(new CRMS.Application.CreditBureau.Commands.ProcessLoanCreditChecksCommand(applicationId, userId), CancellationToken.None);
            return result.IsSuccess
                ? ApiResponse.Ok()
                : ApiResponse.Fail(result.Error ?? "Failed to run credit checks");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running credit checks for application {Id}", applicationId);
            return ApiResponse.Fail("Failed to run credit checks: " + ex.Message);
        }
    }

    private static string GetScoreGrade(int? score)
    {
        if (!score.HasValue) return "N/A";
        return score.Value switch
        {
            >= 750 => "Excellent",
            >= 700 => "Good",
            >= 650 => "Fair",
            _ => "Poor"
        };
    }

    private static string MaskBvn(string? bvn)
    {
        if (string.IsNullOrWhiteSpace(bvn)) return "N/A";
        if (bvn.Length <= 4) return new string('*', bvn.Length);
        return $"*******{bvn[^4..]}";
    }

    public async Task<List<BankStatementInfo>> GetBankStatementsAsync(Guid applicationId)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.StatementAnalysis.Queries.GetStatementsByLoanApplicationHandler>();
            var result = await handler.Handle(new CRMS.Application.StatementAnalysis.Queries.GetStatementsByLoanApplicationQuery(applicationId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
                return new List<BankStatementInfo>();

            return result.Data.Select(s => new BankStatementInfo
            {
                Id = s.Id,
                AccountNumber = s.AccountNumber,
                AccountName = s.AccountName,
                BankName = s.BankName,
                PeriodStart = s.PeriodStart,
                PeriodEnd = s.PeriodEnd,
                MonthsCovered = s.MonthsCovered,
                OpeningBalance = s.OpeningBalance,
                ClosingBalance = s.ClosingBalance,
                Source = s.Source,
                IsInternal = s.IsInternal,
                TrustWeight = s.TrustWeight,
                AnalysisStatus = s.AnalysisStatus,
                VerificationStatus = s.VerificationStatus,
                TransactionCount = s.TransactionCount,
                OriginalFileName = s.OriginalFileName,
                TotalCredits = s.CashflowSummary?.TotalCredits,
                TotalDebits = s.CashflowSummary?.TotalDebits,
                AverageMonthlyBalance = s.CashflowSummary?.AverageMonthlyBalance,
                NetMonthlyCashflow = s.CashflowSummary?.NetCashflow,
                BouncedTransactions = s.CashflowSummary?.BouncedTransactionCount,
                GamblingTransactions = s.CashflowSummary?.GamblingTransactionCount
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bank statements for application {Id}", applicationId);
            return new List<BankStatementInfo>();
        }
    }

    public async Task<ApiResponse<StatementUploadResult>> UploadExternalStatementAsync(Guid applicationId, UploadExternalStatementRequest request, Guid userId)
    {
        try
        {
            string? filePath = null;
            string? originalFileName = null;
            byte[]? fileBytes = null;

            if (request.File != null)
            {
                // Read into memory once — used for both storage upload and parsing
                const long maxSize = 10 * 1024 * 1024; // 10 MB
                using var ms = new System.IO.MemoryStream();
                using (var readStream = request.File.OpenReadStream(maxAllowedSize: maxSize))
                    await readStream.CopyToAsync(ms);
                fileBytes = ms.ToArray();

                var fileStorage = _sp.GetRequiredService<IFileStorageService>();
                var containerName = $"applications/{applicationId}/statements";
                using var uploadStream = new System.IO.MemoryStream(fileBytes);
                filePath = await fileStorage.UploadAsync(containerName, request.File.Name, uploadStream, request.File.ContentType);
                originalFileName = request.File.Name;
            }

            var handler = _sp.GetRequiredService<CRMS.Application.StatementAnalysis.Commands.UploadStatementHandler>();
            var command = new CRMS.Application.StatementAnalysis.Commands.UploadStatementCommand(
                request.AccountNumber,
                request.AccountName,
                request.BankName,
                request.PeriodFrom,
                request.PeriodTo,
                request.OpeningBalance,
                request.ClosingBalance,
                CRMS.Domain.Enums.StatementFormat.PDF,
                CRMS.Domain.Enums.StatementSource.ManualUpload,
                userId,
                originalFileName,
                filePath,
                applicationId
            );
            var result = await handler.Handle(command, CancellationToken.None);
            if (!result.IsSuccess)
                return ApiResponse<StatementUploadResult>.Fail(result.Error ?? "Failed to upload statement");

            var uploadResult = new StatementUploadResult { StatementId = result.Data!.Id };

            // Parse transactions from the file if one was provided
            if (fileBytes != null && request.File != null)
            {
                var parser = _sp.GetRequiredService<StatementFileParserService>();
                using var parseStream = new System.IO.MemoryStream(fileBytes);
                var parseResult = parser.Parse(parseStream, request.File.Name, request.PeriodFrom, request.PeriodTo, request.OpeningBalance);

                uploadResult.ParsedTransactions = parseResult.Transactions;
                uploadResult.ParseMessage = parseResult.Success && parseResult.Transactions.Any()
                    ? $"{parseResult.Transactions.Count} transactions parsed from {parseResult.DetectedFormat}" +
                      (parseResult.SkippedRows > 0 ? $" ({parseResult.SkippedRows} rows skipped)" : "")
                    : parseResult.Error ?? "File saved — no transactions could be extracted automatically. Enter them manually.";
            }

            return ApiResponse<StatementUploadResult>.Ok(uploadResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading external statement for application {Id}", applicationId);
            return ApiResponse<StatementUploadResult>.Fail("Failed to upload statement");
        }
    }

    public async Task<ApiResponse> AddStatementTransactionsAsync(Guid statementId, List<StatementTransactionRow> rows)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.StatementAnalysis.Commands.AddTransactionsHandler>();
            var transactions = rows.Select(r =>
            {
                bool isDebit = (r.DebitAmount ?? 0m) > 0;
                return new CRMS.Application.StatementAnalysis.Commands.TransactionInput(
                    r.Date,
                    r.Description,
                    isDebit ? r.DebitAmount!.Value : r.CreditAmount!.Value,
                    isDebit ? CRMS.Domain.Enums.StatementTransactionType.Debit : CRMS.Domain.Enums.StatementTransactionType.Credit,
                    r.RunningBalance,
                    r.Reference
                );
            }).ToList();

            var command = new CRMS.Application.StatementAnalysis.Commands.AddTransactionsCommand(statementId, transactions);
            var result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess
                ? ApiResponse.Ok()
                : ApiResponse.Fail(result.Error ?? "Failed to add transactions");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding transactions to statement {Id}", statementId);
            return ApiResponse.Fail("Failed to add transactions");
        }
    }

    public async Task<ApiResponse> VerifyStatementAsync(Guid statementId, Guid userId, string? notes = null)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.StatementAnalysis.Commands.VerifyStatementHandler>();
            var result = await handler.Handle(new CRMS.Application.StatementAnalysis.Commands.VerifyStatementCommand(statementId, userId, notes), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to verify statement");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying statement {Id}", statementId);
            return ApiResponse.Fail("Failed to verify statement");
        }
    }

    public async Task<ApiResponse> RejectStatementAsync(Guid statementId, Guid userId, string reason)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.StatementAnalysis.Commands.RejectStatementHandler>();
            var result = await handler.Handle(new CRMS.Application.StatementAnalysis.Commands.RejectStatementCommand(statementId, userId, reason), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to reject statement");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting statement {Id}", statementId);
            return ApiResponse.Fail("Failed to reject statement");
        }
    }

    public async Task<ApiResponse> AnalyzeStatementAsync(Guid statementId)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.StatementAnalysis.Commands.AnalyzeStatementHandler>();
            var result = await handler.Handle(new CRMS.Application.StatementAnalysis.Commands.AnalyzeStatementCommand(statementId), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to analyze statement");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing statement {Id}", statementId);
            return ApiResponse.Fail("Failed to analyze statement");
        }
    }

    public async Task<List<StatementTransactionInfo>> GetStatementTransactionsAsync(Guid statementId)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.StatementAnalysis.Queries.GetStatementTransactionsHandler>();
            var result = await handler.Handle(new CRMS.Application.StatementAnalysis.Queries.GetStatementTransactionsQuery(statementId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
                return new List<StatementTransactionInfo>();

            return result.Data.Select(t => new StatementTransactionInfo
            {
                Id = t.Id,
                Date = t.Date,
                Description = t.Description,
                Amount = t.Amount,
                Type = t.Type,
                RunningBalance = t.RunningBalance,
                Reference = t.Reference,
                Category = t.Category,
                CategoryConfidence = t.CategoryConfidence,
                IsRecurring = t.IsRecurring
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching transactions for statement {Id}", statementId);
            return new List<StatementTransactionInfo>();
        }
    }

    public async Task<ApiResponse<CRMS.Web.Intranet.Models.CustomerInfo>> FetchCorporateDataAsync(string accountNumber)
    {
        try
        {
            var coreBanking = _sp.GetRequiredService<CRMS.Domain.Interfaces.ICoreBankingService>();
            var customerResult = await coreBanking.GetCustomerByAccountNumberAsync(accountNumber);
            if (customerResult.IsFailure)
                return ApiResponse<CRMS.Web.Intranet.Models.CustomerInfo>.Fail(customerResult.Error ?? "Account not found");

            var customer = customerResult.Value;
            if (customer.CustomerType != CRMS.Domain.Interfaces.CustomerType.Corporate)
                return ApiResponse<CRMS.Web.Intranet.Models.CustomerInfo>.Fail("Account is not a corporate account");

            // Fetch signatories from core banking (directors now come from SmartComply CAC)
            var signatoriesResult = await coreBanking.GetSignatoriesAsync(accountNumber);
            var signatories = signatoriesResult.IsSuccess
                ? signatoriesResult.Value.Select(s => new CRMS.Web.Intranet.Models.SignatoryInput
                  {
                      SignatoryId = s.SignatoryId,
                      FullName = s.FullName,
                      BVN = s.BVN,
                      Email = s.Email,
                      PhoneNumber = s.PhoneNumber,
                      Designation = s.Designation,
                      MandateType = s.MandateType
                  }).ToList()
                : new List<CRMS.Web.Intranet.Models.SignatoryInput>();

            // Fetch directors from core banking (for discrepancy comparison against SmartComply CAC)
            var directorsResult = await coreBanking.GetDirectorsAsync(customer.CustomerId);
            var cbsDirectors = directorsResult.IsSuccess
                ? directorsResult.Value.Select(d => new CRMS.Web.Intranet.Models.CbsDirectorInfo
                  {
                      FullName = d.FullName,
                      BVN = d.BVN,
                      Email = d.Email,
                      PhoneNumber = d.PhoneNumber,
                      DateOfBirth = d.DateOfBirth?.ToString("dd-MM-yyyy"),
                      Address = d.Address
                  }).ToList()
                : new List<CRMS.Web.Intranet.Models.CbsDirectorInfo>();

            var info = new CRMS.Web.Intranet.Models.CustomerInfo
            {
                AccountNumber = accountNumber,
                CompanyName = customer.FullName,
                // RC number intentionally left blank — user enters it for SmartComply lookup
                RegistrationNumber = string.Empty,
                Industry = string.Empty,
                IncorporationDate = null,
                Address = customer.Address ?? string.Empty,
                Email = customer.Email ?? string.Empty,
                Phone = customer.PhoneNumber ?? string.Empty,
                Signatories = signatories,
                CbsDirectors = cbsDirectors
            };
            return ApiResponse<CRMS.Web.Intranet.Models.CustomerInfo>.Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching corporate data for {AccountNumber}", accountNumber);
            return ApiResponse<CRMS.Web.Intranet.Models.CustomerInfo>.Fail("Failed to fetch customer data");
        }
    }

    public async Task<ApiResponse<CRMS.Web.Intranet.Models.CacLookupResult>> FetchCacDirectorsAsync(string rcNumber)
    {
        try
        {
            var smartComply = _sp.GetRequiredService<CRMS.Domain.Interfaces.ISmartComplyProvider>();
            var result = await smartComply.VerifyCacAdvancedAsync(rcNumber.Trim());
            if (result.IsFailure)
                return ApiResponse<CRMS.Web.Intranet.Models.CacLookupResult>.Fail(result.Error ?? "CAC lookup failed");

            var cac = result.Value;
            var lookupResult = new CRMS.Web.Intranet.Models.CacLookupResult
            {
                CompanyName = cac.CompanyName,
                RcNumber = cac.RcNumber,
                Status = cac.Status,
                RegistrationDate = cac.RegistrationDate,
                EntityType = cac.CompanyType,
                Address = cac.Address,
                City = cac.City,
                State = cac.State,
                CompanyId = cac.CompanyId,
                Directors = cac.Directors.Select(d => new CRMS.Web.Intranet.Models.CacDirectorEntry
                {
                    Id = d.Id,
                    FullName = d.FullName ?? $"{d.FirstName} {d.Surname}".Trim(),
                    Surname = d.Surname,
                    FirstName = d.FirstName,
                    OtherName = d.OtherName,
                    Gender = d.Gender,
                    DateOfBirth = d.DateOfBirth,
                    Occupation = d.Occupation,
                    Nationality = d.Nationality,
                    Email = d.Email,
                    PhoneNumber = d.PhoneNumber,
                    Address = d.Address,
                    City = d.City,
                    State = d.State,
                    IsChairman = d.IsChairman,
                    AffiliateType = d.AffiliateType,
                    TypeOfShares = d.TypeOfShares,
                    NumSharesAlloted = d.NumSharesAlloted,
                    DateOfAppointment = d.DateOfAppointment,
                    BvnInput = string.Empty
                }).ToList()
            };
            return ApiResponse<CRMS.Web.Intranet.Models.CacLookupResult>.Ok(lookupResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching CAC directors for RC {RcNumber}", rcNumber);
            return ApiResponse<CRMS.Web.Intranet.Models.CacLookupResult>.Fail("Failed to fetch directors from CAC");
        }
    }

    public async Task<ApiResponse> UpdatePartyInfoAsync(Guid applicationId, Guid partyId, string? bvn, decimal? shareholdingPercent, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.LoanApplication.Commands.UpdatePartyInfoHandler>();
            var result = await handler.Handle(new CRMS.Application.LoanApplication.Commands.UpdatePartyInfoCommand(applicationId, partyId, bvn, shareholdingPercent, userId), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to update party info");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating party info for party {PartyId}", partyId);
            return ApiResponse.Fail("Failed to update party info");
        }
    }

    public async Task<CollateralDetailDto?> GetCollateralDetailAsync(Guid collateralId)
    {
        try
        {
            GetCollateralByIdHandler handler = _sp.GetRequiredService<GetCollateralByIdHandler>();
            ApplicationResult<CollateralDto> result = await handler.Handle(new GetCollateralByIdQuery(collateralId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return null;
            }
            CollateralDto c = result.Data;
            return new CollateralDetailDto
            {
                Id = c.Id,
                CollateralReference = c.CollateralReference,
                Type = c.Type,
                Status = c.Status,
                PerfectionStatus = c.PerfectionStatus,
                Description = c.Description,
                AssetIdentifier = c.AssetIdentifier,
                Location = c.Location,
                OwnerName = c.OwnerName,
                OwnershipType = c.OwnershipType,
                MarketValue = c.MarketValue,
                ForcedSaleValue = c.ForcedSaleValue,
                AcceptableValue = c.AcceptableValue,
                HaircutPercentage = c.HaircutPercentage,
                Currency = c.Currency,
                LastValuationDate = c.LastValuationDate,
                LienType = c.LienType,
                LienReference = c.LienReference,
                LienRegistrationDate = c.LienRegistrationDate,
                IsInsured = c.IsInsured,
                InsurancePolicyNumber = c.InsurancePolicyNumber,
                InsuredValue = c.InsuredValue,
                InsuranceExpiryDate = c.InsuranceExpiryDate,
                CreatedAt = c.CreatedAt,
                ApprovedAt = c.ApprovedAt,
                RejectionReason = c.RejectionReason,
                Documents = c.Documents.Select(d => new CollateralDocumentInfo
                {
                    Id = d.Id,
                    DocumentType = d.DocumentType,
                    FileName = d.FileName,
                    FileSizeBytes = d.FileSizeBytes,
                    IsVerified = d.IsVerified,
                    UploadedAt = d.UploadedAt
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching collateral detail for {CollateralId}", collateralId);
            return null;
        }
    }

    public async Task<ApiResponse> UpdateCollateralAsync(Guid collateralId, UpdateCollateralRequest request)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Collateral.Commands.UpdateCollateralHandler>();
            var command = new Application.Collateral.Commands.UpdateCollateralCommand(
                collateralId,
                Enum.Parse<CollateralType>(request.Type, ignoreCase: true),
                request.Description,
                request.AssetIdentifier,
                request.Location,
                request.OwnerName,
                request.OwnershipType);
            var result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to update collateral");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating collateral {CollateralId}", collateralId);
            return ApiResponse.Fail("Failed to update collateral");
        }
    }

    public async Task<GuarantorDetailDto?> GetGuarantorDetailAsync(Guid guarantorId)
    {
        try
        {
            GetGuarantorByIdHandler handler = _sp.GetRequiredService<GetGuarantorByIdHandler>();
            ApplicationResult<GuarantorDto> result = await handler.Handle(new GetGuarantorByIdQuery(guarantorId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return null;
            }
            GuarantorDto g = result.Data;
            return new GuarantorDetailDto
            {
                Id = g.Id,
                GuarantorReference = g.GuarantorReference,
                Type = g.Type,
                Status = g.Status,
                GuaranteeType = g.GuaranteeType,
                FullName = g.FullName,
                BVN = g.BVN,
                Email = g.Email,
                Phone = g.Phone,
                Address = g.Address,
                RelationshipToApplicant = g.RelationshipToApplicant,
                IsDirector = g.IsDirector,
                IsShareholder = g.IsShareholder,
                ShareholdingPercentage = g.ShareholdingPercentage,
                Occupation = g.Occupation,
                EmployerName = g.EmployerName,
                MonthlyIncome = g.MonthlyIncome,
                DeclaredNetWorth = g.DeclaredNetWorth,
                VerifiedNetWorth = g.VerifiedNetWorth,
                GuaranteeLimit = g.GuaranteeLimit,
                IsUnlimited = g.IsUnlimited,
                GuaranteeStartDate = g.GuaranteeStartDate,
                GuaranteeEndDate = g.GuaranteeEndDate,
                CreditScore = g.CreditScore,
                CreditScoreGrade = g.CreditScoreGrade,
                CreditCheckDate = g.CreditCheckDate,
                HasCreditIssues = g.HasCreditIssues,
                CreditIssuesSummary = g.CreditIssuesSummary,
                ExistingGuaranteeCount = g.ExistingGuaranteeCount,
                TotalExistingGuarantees = g.TotalExistingGuarantees,
                HasSignedGuaranteeAgreement = g.HasSignedGuaranteeAgreement,
                AgreementSignedDate = g.AgreementSignedDate,
                CreatedAt = g.CreatedAt,
                ApprovedAt = g.ApprovedAt,
                RejectionReason = g.RejectionReason
            };
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching guarantor detail for {GuarantorId}", guarantorId);
            return null;
        }
    }

    public async Task<ApiResponse> UpdateGuarantorAsync(Guid guarantorId, UpdateGuarantorRequest request)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Guarantor.Commands.UpdateGuarantorHandler>();
            var command = new Application.Guarantor.Commands.UpdateGuarantorCommand(
                guarantorId,
                Enum.Parse<GuaranteeType>(request.GuaranteeType, ignoreCase: true),
                request.FullName,
                request.BVN,
                request.Email,
                request.Phone,
                request.Address,
                request.Relationship,
                request.IsDirector,
                request.IsShareholder,
                request.ShareholdingPercentage,
                request.Occupation,
                request.EmployerName,
                request.MonthlyIncome,
                request.DeclaredNetWorth,
                request.GuaranteeLimit);
            var result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to update guarantor");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating guarantor {GuarantorId}", guarantorId);
            return ApiResponse.Fail("Failed to update guarantor");
        }
    }

    public ApplicationService(IServiceProvider sp, IReportingService reporting, ILogger<ApplicationService> logger, IOptions<BankSettings> bankSettings)
    {
        _sp = sp;
        _reporting = reporting;
        _logger = logger;
        _bankSettings = bankSettings.Value;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        try
        {
            DashboardSummaryDto data = await _reporting.GetDashboardSummaryAsync();
            
            // Get applications by status breakdown
            var applicationsByStatus = new List<ApplicationByStatus>();
            var total = data.LoanFunnel.Submitted + data.LoanFunnel.InReview + data.LoanFunnel.Approved + data.LoanFunnel.Rejected + data.LoanFunnel.Disbursed;
            if (total > 0)
            {
                if (data.LoanFunnel.InReview > 0)
                    applicationsByStatus.Add(new ApplicationByStatus { Status = "In Progress", Count = data.LoanFunnel.InReview, Percentage = Math.Round((decimal)data.LoanFunnel.InReview / total * 100, 0) });
                if (data.PendingActions.PendingApplications > 0)
                    applicationsByStatus.Add(new ApplicationByStatus { Status = "Pending Review", Count = data.PendingActions.PendingApplications, Percentage = Math.Round((decimal)data.PendingActions.PendingApplications / total * 100, 0) });
                if (data.LoanFunnel.Approved + data.LoanFunnel.Disbursed > 0)
                    applicationsByStatus.Add(new ApplicationByStatus { Status = "Approved", Count = data.LoanFunnel.Approved + data.LoanFunnel.Disbursed, Percentage = Math.Round((decimal)(data.LoanFunnel.Approved + data.LoanFunnel.Disbursed) / total * 100, 0) });
                if (data.LoanFunnel.Rejected > 0)
                    applicationsByStatus.Add(new ApplicationByStatus { Status = "Rejected", Count = data.LoanFunnel.Rejected, Percentage = Math.Round((decimal)data.LoanFunnel.Rejected / total * 100, 0) });
            }
            
            // Get recent activity from workflow transition logs
            var recentActivities = new List<RecentActivity>();
            try
            {
                var auditLogs = await GetAuditLogsAsync(null, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
                recentActivities = auditLogs
                    .Where(l => l.EntityType == "LoanApplication" || l.EntityType == "Workflow")
                    .Take(6)
                    .Select(l => new RecentActivity
                    {
                        ApplicationId = Guid.TryParse(l.EntityId, out var id) ? id : Guid.Empty,
                        ApplicationNumber = l.EntityId ?? "",
                        Action = l.Action.ToLower().Replace("_", " "),
                        PerformedBy = l.UserName,
                        Timestamp = l.Timestamp
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch recent activities for dashboard");
            }
            
            return new DashboardSummary
            {
                TotalApplications = data.LoanFunnel.Submitted + data.LoanFunnel.InReview + data.LoanFunnel.Approved,
                PendingApplications = data.PendingActions.PendingApplications,
                ApprovedThisMonth = data.LoanFunnel.Approved,
                RejectedThisMonth = data.LoanFunnel.Rejected,
                TotalDisbursedAmount = data.LoanFunnel.DisbursedAmount,
                AverageProcessingDays = data.Performance.AverageProcessingTimeDays,
                MyPendingTasks = data.PendingActions.PendingApplications,
                OverdueApplications = data.PendingActions.OverdueSLAs,
                ApplicationsGrowthPercent = (int)data.Performance.MonthOverMonthGrowth,
                ApprovalRateChange = 0, // Would need historical data to calculate
                ApplicationsByStatus = applicationsByStatus,
                RecentActivities = recentActivities
            };
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching dashboard summary");
            return new DashboardSummary();
        }
    }

    public async Task<List<PendingTask>> GetMyPendingTasksAsync(Guid userId)
    {
        try
        {
            GetMyWorkflowQueueHandler handler = _sp.GetRequiredService<GetMyWorkflowQueueHandler>();
            ApplicationResult<List<WorkflowInstanceSummaryDto>> result = await handler.Handle(new GetMyWorkflowQueueQuery(userId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<PendingTask>();
            }

            var loanRepo = _sp.GetRequiredService<ILoanApplicationRepository>();
            var tasks = new List<PendingTask>();
            foreach (var t in result.Data)
            {
                var loan = await loanRepo.GetByIdAsync(t.LoanApplicationId);
                tasks.Add(new PendingTask
                {
                    ApplicationId = t.LoanApplicationId,
                    ApplicationNumber = t.ApplicationNumber,
                    CustomerName = t.CustomerName,
                    Stage = t.CurrentStatus,
                    RequiredAction = t.CurrentStageDisplayName,
                    DueDate = (t.SLADueAt ?? DateTime.UtcNow.AddDays(3.0)),
                    Amount = loan?.RequestedAmount.Amount ?? 0m,
                    ProductName = loan?.ProductCode ?? ""
                });
            }
            return tasks;
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching pending tasks");
            return new List<PendingTask>();
        }
    }

    public Task<(List<LoanApplicationSummary> Items, int TotalCount)> GetApplicationsByStatusAsync(string? status)
        => GetApplicationsByStatusAsync(status, null, null, null);

    public async Task<(List<LoanApplicationSummary> Items, int TotalCount)> GetApplicationsByStatusAsync(
        string? status, Guid? userLocationId, string? userRole, Guid? userId)
    {
        try
        {
            if (string.IsNullOrEmpty(status))
            {
                return (Items: new List<LoanApplicationSummary>(), TotalCount: 0);
            }
            LoanApplicationStatus statusEnum = Enum.Parse<LoanApplicationStatus>(status, ignoreCase: true);
            GetLoanApplicationsByStatusHandler handler = _sp.GetRequiredService<GetLoanApplicationsByStatusHandler>();
            ApplicationResult<List<LoanApplicationSummaryDto>> result = await handler.Handle(
                new GetLoanApplicationsByStatusQuery(statusEnum, userLocationId, userRole, userId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return (Items: new List<LoanApplicationSummary>(), TotalCount: 0);
            }
            List<LoanApplicationSummary> items = result.Data.Select((LoanApplicationSummaryDto a) => new LoanApplicationSummary
            {
                Id = a.Id,
                ApplicationNumber = a.ApplicationNumber,
                CustomerName = a.CustomerName,
                ProductName = a.ProductCode,
                RequestedAmount = a.RequestedAmount,
                Status = a.Status,
                CreatedAt = a.CreatedAt
            }).ToList();
            return (Items: items, TotalCount: items.Count);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching applications by status");
            return (Items: new List<LoanApplicationSummary>(), TotalCount: 0);
        }
    }

    public async Task<List<LoanApplicationSummary>> GetMyApplicationsAsync(Guid userId)
    {
        try
        {
            GetMyLoanApplicationsHandler handler = _sp.GetRequiredService<GetMyLoanApplicationsHandler>();
            ApplicationResult<List<LoanApplicationSummaryDto>> result = await handler.Handle(new GetMyLoanApplicationsQuery(userId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<LoanApplicationSummary>();
            }
            return result.Data.Select((LoanApplicationSummaryDto a) => new LoanApplicationSummary
            {
                Id = a.Id,
                ApplicationNumber = a.ApplicationNumber,
                CustomerName = a.CustomerName,
                ProductName = a.ProductCode,
                RequestedAmount = a.RequestedAmount,
                Status = a.Status,
                CreatedAt = a.CreatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching my applications");
            return new List<LoanApplicationSummary>();
        }
    }

    public async Task<LoanApplicationDetail?> GetApplicationDetailAsync(Guid id)
    {
        try
        {
            GetLoanApplicationByIdHandler handler = _sp.GetRequiredService<GetLoanApplicationByIdHandler>();
            ApplicationResult<LoanApplicationDto> result = await handler.Handle(new GetLoanApplicationByIdQuery(id), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return null;
            }
            LoanApplicationDto app = result.Data;
            LoanApplicationDetail loanApplicationDetail = new LoanApplicationDetail
            {
                Id = app.Id,
                ApplicationNumber = app.ApplicationNumber,
                Status = app.Status,
                Customer = new CRMS.Web.Intranet.Models.CustomerInfo
                {
                    AccountNumber = app.AccountNumber,
                    CompanyName = app.CustomerName,
                    RegistrationNumber = app.RegistrationNumber ?? string.Empty,
                    IncorporationDate = app.IncorporationDate
                },
                Loan = new CRMS.Web.Intranet.Models.LoanInfo
                {
                    ProductId = app.LoanProductId,
                    ProductName = app.ProductCode,
                    RequestedAmount = app.RequestedAmount,
                    ApprovedAmount = app.ApprovedAmount,
                    InterestRate = app.InterestRatePerAnnum,
                    InterestRateType = app.InterestRateType,
                    TenorMonths = app.RequestedTenorMonths,
                    Purpose = (app.Purpose ?? "")
                },
                Directors = (from p in app.Parties
                             where p.PartyType == "Director"
                             select new PartyInfo
                             {
                                 Id = p.Id,
                                 Name = p.FullName,
                                 RawBVN = p.BVN,
                                 BvnMasked = MaskBvn(p.BVN),
                                 Position = (p.Designation ?? ""),
                                 PartyType = p.PartyType,
                                 ShareholdingPercentage = p.ShareholdingPercent
                             }).ToList(),
                Signatories = (from p in app.Parties
                               where p.PartyType == "Signatory"
                               select new PartyInfo
                               {
                                   Id = p.Id,
                                   Name = p.FullName,
                                   RawBVN = p.BVN,
                                   BvnMasked = MaskBvn(p.BVN),
                                   Position = (p.Designation ?? ""),
                                   PartyType = p.PartyType,
                                   MandateType = p.Designation
                               }).ToList(),
                Documents = app.Documents.Select((LoanApplicationDocumentDto d) => new DocumentInfo
                {
                    Id = d.Id,
                    Name = d.FileName,
                    Category = d.Category,
                    Status = d.Status,
                    UploadedAt = d.UploadedAt,
                    SizeBytes = d.FileSize
                }).ToList()
            };
            LoanApplicationDetail loanApplicationDetail2 = loanApplicationDetail;
            loanApplicationDetail2.Collaterals = await GetCollateralsForApplicationAsync(id, app.RequestedAmount);
            LoanApplicationDetail loanApplicationDetail3 = loanApplicationDetail;
            loanApplicationDetail3.Guarantors = await GetGuarantorsForApplicationAsync(id);
            LoanApplicationDetail loanApplicationDetail4 = loanApplicationDetail;
            loanApplicationDetail4.FinancialStatements = await GetFinancialStatementsForApplicationAsync(id);
            loanApplicationDetail.CreatedAt = app.CreatedAt;
            loanApplicationDetail.LastUpdatedAt = app.ModifiedAt;
            loanApplicationDetail.WorkflowHistory = await GetWorkflowHistoryForApplicationAsync(id);
            loanApplicationDetail.Advisory = await GetAdvisoryForApplicationAsync(id);
            loanApplicationDetail.Committee = await GetCommitteeForApplicationAsync(id);
            loanApplicationDetail.Comments = await GetCommentsForApplicationAsync(id);
            return loanApplicationDetail;
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching application detail for {Id}", id);
            return null;
        }
    }

    private async Task<List<CollateralInfo>> GetCollateralsForApplicationAsync(Guid applicationId, decimal loanAmount = 0m)
    {
        try
        {
            var summaryHandler = _sp.GetRequiredService<GetCollateralByLoanApplicationHandler>();
            var summaryResult = await summaryHandler.Handle(new GetCollateralByLoanApplicationQuery(applicationId), CancellationToken.None);
            if (!summaryResult.IsSuccess || summaryResult.Data == null)
                return new List<CollateralInfo>();

            var detailHandler = _sp.GetRequiredService<Application.Collateral.Queries.GetCollateralByIdHandler>();
            var items = new List<CollateralInfo>();
            foreach (var summary in summaryResult.Data)
            {
                var detailResult = await detailHandler.Handle(new Application.Collateral.Queries.GetCollateralByIdQuery(summary.Id), CancellationToken.None);
                if (detailResult.IsSuccess && detailResult.Data != null)
                {
                    var c = detailResult.Data;
                    var acceptableValue = c.AcceptableValue.GetValueOrDefault();
                    var ltv = loanAmount > 0 && acceptableValue > 0
                        ? Math.Round((loanAmount / acceptableValue) * 100, 2)
                        : 0m;
                    items.Add(new CollateralInfo
                    {
                        Id = c.Id,
                        Type = c.Type,
                        Description = c.Description,
                        MarketValue = c.MarketValue.GetValueOrDefault(),
                        ForcedSaleValue = c.ForcedSaleValue.GetValueOrDefault(),
                        LoanToValue = ltv,
                        Status = c.Status,
                        LastValuationDate = c.LastValuationDate
                    });
                }
                else
                {
                    // Fall back to summary data if detail fetch fails
                    items.Add(new CollateralInfo
                    {
                        Id = summary.Id,
                        Type = summary.Type,
                        Description = summary.Description,
                        MarketValue = summary.AcceptableValue.GetValueOrDefault(),
                        ForcedSaleValue = summary.AcceptableValue.GetValueOrDefault(),
                        LoanToValue = 0m,
                        Status = summary.Status,
                        LastValuationDate = summary.CreatedAt
                    });
                }
            }
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching collaterals for application {Id}", applicationId);
            return new List<CollateralInfo>();
        }
    }

    private async Task<List<GuarantorInfo>> GetGuarantorsForApplicationAsync(Guid applicationId)
    {
        try
        {
            GetGuarantorsByLoanApplicationHandler handler = _sp.GetRequiredService<GetGuarantorsByLoanApplicationHandler>();
            ApplicationResult<List<GuarantorSummaryDto>> result = await handler.Handle(new GetGuarantorsByLoanApplicationQuery(applicationId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<GuarantorInfo>();
            }
            return result.Data.Select((GuarantorSummaryDto g) => new GuarantorInfo
            {
                Id = g.Id,
                Name = g.FullName,
                Relationship = g.Type,
                GuaranteeAmount = g.GuaranteeLimit.GetValueOrDefault(),
                Status = g.Status,
                HasBureauReport = g.CreditScore.HasValue
            }).ToList();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching guarantors for application {Id}", applicationId);
            return new List<GuarantorInfo>();
        }
    }

    private async Task<List<FinancialStatementInfo>> GetFinancialStatementsForApplicationAsync(Guid applicationId)
    {
        try
        {
            GetFinancialStatementsByLoanApplicationHandler handler = _sp.GetRequiredService<GetFinancialStatementsByLoanApplicationHandler>();
            ApplicationResult<List<FinancialStatementSummaryDto>> result = await handler.Handle(new GetFinancialStatementsByLoanApplicationQuery(applicationId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<FinancialStatementInfo>();
            }
            return result.Data.Select((FinancialStatementSummaryDto fs) => new FinancialStatementInfo
            {
                Id = fs.Id,
                Year = fs.FinancialYear,
                YearEndDate = fs.SubmittedAt,
                YearType = fs.YearType,
                Status = fs.Status,
                TotalAssets = fs.TotalAssets.GetValueOrDefault(),
                Revenue = fs.TotalRevenue.GetValueOrDefault(),
                NetProfit = fs.NetProfit.GetValueOrDefault()
            }).ToList();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching financial statements for application {Id}", applicationId);
            return new List<FinancialStatementInfo>();
        }
    }

    private async Task<List<WorkflowHistoryItem>> GetWorkflowHistoryForApplicationAsync(Guid applicationId)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetWorkflowByLoanApplicationHandler>();
            var result = await handler.Handle(new GetWorkflowByLoanApplicationQuery(applicationId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
                return [];

            return result.Data.RecentHistory.Select(t => new WorkflowHistoryItem
            {
                FromStage = t.FromStatus ?? "",
                ToStage = t.ToStatus,
                Action = t.Action,
                PerformedBy = t.PerformedByUserId.ToString(),
                Timestamp = t.PerformedAt,
                Comments = t.Comment
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching workflow history for application {Id}", applicationId);
            return [];
        }
    }

    private async Task<AdvisoryInfo?> GetAdvisoryForApplicationAsync(Guid applicationId)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetLatestAdvisoryByLoanApplicationHandler>();
            var result = await handler.Handle(new GetLatestAdvisoryByLoanApplicationQuery(applicationId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
                return null;

            var adv = result.Data;
            return new AdvisoryInfo
            {
                OverallScore = adv.OverallScore,
                RiskRating = adv.OverallRating ?? "",
                GeneratedAt = adv.GeneratedAt,
                Recommendations = !string.IsNullOrEmpty(adv.Recommendation) ? [adv.Recommendation] : [],
                ScoreBreakdown = adv.RiskScores.Select(c => new ScoreCategory
                {
                    Category = c.Category,
                    Score = c.Score,
                    MaxScore = c.Weight > 0 ? (int)(c.Score / c.Weight * 100) : 100,
                    Weight = c.Weight
                }).ToList(),
                RedFlags = adv.RedFlags.ToList(),
                Strengths = !string.IsNullOrEmpty(adv.StrengthsAnalysis) ? [adv.StrengthsAnalysis] : []
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching advisory for application {Id}", applicationId);
            return null;
        }
    }

    private async Task<CommitteeInfo?> GetCommitteeForApplicationAsync(Guid applicationId)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetCommitteeReviewByLoanApplicationHandler>();
            var result = await handler.Handle(new GetCommitteeReviewByLoanApplicationQuery(applicationId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
                return null;

            var review = result.Data;
            return new CommitteeInfo
            {
                ReviewId = review.Id,
                CommitteeType = review.CommitteeType,
                Status = review.Status,
                Decision = review.FinalDecision,
                DecisionComments = review.DecisionRationale,
                DecisionDate = review.DecisionAt,
                Members = review.Members.Select(m => new CommitteeMemberVote
                {
                    UserId = m.UserId,
                    Name = m.UserName,
                    Role = m.Role,
                    Vote = m.Vote,
                    VotedAt = m.VotedAt,
                    Comments = m.VoteComment
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching committee review for application {Id}", applicationId);
            return null;
        }
    }

    private async Task<List<CommentInfo>> GetCommentsForApplicationAsync(Guid applicationId)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetCommitteeReviewByLoanApplicationHandler>();
            var result = await handler.Handle(new GetCommitteeReviewByLoanApplicationQuery(applicationId), CancellationToken.None);
            if (!result.IsSuccess || result.Data?.RecentComments == null)
                return [];

            return result.Data.RecentComments.Select(c => new CommentInfo
            {
                Id = c.Id,
                Author = c.UserName,
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                IsInternal = c.Visibility == "Internal"
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching comments for application {Id}", applicationId);
            return [];
        }
    }

    public async Task<ApiResponse> AddCommentAsync(Guid applicationId, string content, Guid userId)
    {
        try
        {
            var reviewHandler = _sp.GetRequiredService<GetCommitteeReviewByLoanApplicationHandler>();
            var reviewResult = await reviewHandler.Handle(new GetCommitteeReviewByLoanApplicationQuery(applicationId), CancellationToken.None);
            if (!reviewResult.IsSuccess || reviewResult.Data == null)
                return ApiResponse.Fail("No committee review found for this application");

            var handler = _sp.GetRequiredService<AddCommitteeCommentHandler>();
            var result = await handler.Handle(
                new AddCommitteeCommentCommand(reviewResult.Data.Id, userId, content, Domain.Enums.CommentVisibility.Committee),
                CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to add comment");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to application {Id}", applicationId);
            return ApiResponse.Fail("Failed to add comment");
        }
    }

    public async Task<ApiResponse<Guid>> CreateCommitteeReviewAsync(
        Guid loanApplicationId, string applicationNumber, string committeeType,
        int requiredVotes, int minimumApprovalVotes, int deadlineHours, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<CreateCommitteeReviewHandler>();
            var ctEnum = Enum.Parse<CommitteeType>(committeeType, ignoreCase: true);
            var result = await handler.Handle(
                new CreateCommitteeReviewCommand(loanApplicationId, applicationNumber, ctEnum, userId, requiredVotes, minimumApprovalVotes, deadlineHours),
                CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
                return ApiResponse<Guid>.Fail(result.Error ?? "Failed to create committee review");
            return ApiResponse<Guid>.Ok(result.Data.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating committee review for {LoanApplicationId}", loanApplicationId);
            return ApiResponse<Guid>.Fail("Failed to create committee review");
        }
    }

    public async Task<ApiResponse> AddCommitteeMemberAsync(Guid reviewId, Guid memberId, string memberName, string role, bool isChairperson)
    {
        try
        {
            var handler = _sp.GetRequiredService<AddCommitteeMemberHandler>();
            var result = await handler.Handle(
                new AddCommitteeMemberCommand(reviewId, memberId, memberName, role, isChairperson),
                CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to add member");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding committee member to review {ReviewId}", reviewId);
            return ApiResponse.Fail("Failed to add committee member");
        }
    }

    public async Task<List<UserSummary>> GetCommitteeMemberUsersAsync()
    {
        try
        {
            var handler = _sp.GetRequiredService<GetAllUsersHandler>();
            var result = await handler.Handle(new GetAllUsersQuery(), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null) return [];

            return result.Data
                .Where(u => u.Status == "Active" && u.Roles.Contains("CommitteeMember"))
                .Select(u =>
                {
                    var parts = u.FullName.Split(' ', 2);
                    return new UserSummary
                    {
                        Id = u.Id,
                        FirstName = parts.Length > 0 ? parts[0] : "",
                        LastName = parts.Length > 1 ? parts[1] : "",
                        Email = u.Email,
                        Role = "CommitteeMember",
                        IsActive = true
                    };
                }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching committee member users");
            return [];
        }
    }

    // Standing Committee Management
    public async Task<List<StandingCommitteeInfo>> GetStandingCommitteesAsync(bool includeInactive = false)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Committee.Queries.GetAllStandingCommitteesHandler>();
            var result = await handler.Handle(new Application.Committee.Queries.GetAllStandingCommitteesQuery(includeInactive), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null) return [];

            return result.Data.Select(c => new StandingCommitteeInfo
            {
                Id = c.Id,
                Name = c.Name,
                CommitteeType = c.CommitteeType,
                RequiredVotes = c.RequiredVotes,
                MinimumApprovalVotes = c.MinimumApprovalVotes,
                DefaultDeadlineHours = c.DefaultDeadlineHours,
                MinAmountThreshold = c.MinAmountThreshold,
                MaxAmountThreshold = c.MaxAmountThreshold,
                IsActive = c.IsActive,
                Members = c.Members.Select(m => new StandingMemberInfo
                {
                    Id = m.Id,
                    UserId = m.UserId,
                    UserName = m.UserName,
                    Role = m.Role,
                    IsChairperson = m.IsChairperson
                }).ToList()
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching standing committees");
            return [];
        }
    }

    public async Task<ApiResponse<Guid>> CreateStandingCommitteeAsync(
        string name, string committeeType, int requiredVotes, int minimumApprovalVotes,
        int deadlineHours, decimal minAmount, decimal? maxAmount)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Committee.Commands.CreateStandingCommitteeHandler>();
            var ctEnum = Enum.Parse<CommitteeType>(committeeType, ignoreCase: true);
            var result = await handler.Handle(
                new Application.Committee.Commands.CreateStandingCommitteeCommand(
                    name, ctEnum, requiredVotes, minimumApprovalVotes, deadlineHours, minAmount, maxAmount),
                CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
                return ApiResponse<Guid>.Fail(result.Error ?? "Failed to create committee");
            return ApiResponse<Guid>.Ok(result.Data.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating standing committee");
            return ApiResponse<Guid>.Fail("Failed to create standing committee");
        }
    }

    public async Task<ApiResponse> UpdateStandingCommitteeAsync(
        Guid id, string name, int requiredVotes, int minimumApprovalVotes,
        int deadlineHours, decimal minAmount, decimal? maxAmount)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Committee.Commands.UpdateStandingCommitteeHandler>();
            var result = await handler.Handle(
                new Application.Committee.Commands.UpdateStandingCommitteeCommand(
                    id, name, requiredVotes, minimumApprovalVotes, deadlineHours, minAmount, maxAmount),
                CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to update");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating standing committee {Id}", id);
            return ApiResponse.Fail("Failed to update standing committee");
        }
    }

    public async Task<ApiResponse> ToggleStandingCommitteeAsync(Guid id, bool activate)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Committee.Commands.ToggleStandingCommitteeHandler>();
            var result = await handler.Handle(
                new Application.Committee.Commands.ToggleStandingCommitteeCommand(id, activate),
                CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling standing committee {Id}", id);
            return ApiResponse.Fail("Failed to toggle standing committee status");
        }
    }

    public async Task<ApiResponse> AddStandingCommitteeMemberAsync(Guid committeeId, Guid userId, string userName, string role, bool isChairperson)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Committee.Commands.AddStandingCommitteeMemberHandler>();
            var result = await handler.Handle(
                new Application.Committee.Commands.AddStandingCommitteeMemberCommand(committeeId, userId, userName, role, isChairperson),
                CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to add member");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding member to standing committee {Id}", committeeId);
            return ApiResponse.Fail("Failed to add member");
        }
    }

    public async Task<ApiResponse> RemoveStandingCommitteeMemberAsync(Guid committeeId, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Committee.Commands.RemoveStandingCommitteeMemberHandler>();
            var result = await handler.Handle(
                new Application.Committee.Commands.RemoveStandingCommitteeMemberCommand(committeeId, userId),
                CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to remove member");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing member from standing committee {Id}", committeeId);
            return ApiResponse.Fail("Failed to remove member");
        }
    }

    public async Task<StandingCommitteeInfo?> GetStandingCommitteeForAmountAsync(decimal amount)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Committee.Queries.GetStandingCommitteeForAmountHandler>();
            var result = await handler.Handle(new Application.Committee.Queries.GetStandingCommitteeForAmountQuery(amount), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null) return null;

            var c = result.Data;
            return new StandingCommitteeInfo
            {
                Id = c.Id,
                Name = c.Name,
                CommitteeType = c.CommitteeType,
                RequiredVotes = c.RequiredVotes,
                MinimumApprovalVotes = c.MinimumApprovalVotes,
                DefaultDeadlineHours = c.DefaultDeadlineHours,
                MinAmountThreshold = c.MinAmountThreshold,
                MaxAmountThreshold = c.MaxAmountThreshold,
                IsActive = c.IsActive,
                Members = c.Members.Select(m => new StandingMemberInfo
                {
                    Id = m.Id,
                    UserId = m.UserId,
                    UserName = m.UserName,
                    Role = m.Role,
                    IsChairperson = m.IsChairperson
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching standing committee for amount {Amount}", amount);
            return null;
        }
    }

    public async Task<ApiResponse<Guid>> CreateApplicationAsync(CreateApplicationRequest request, Guid productId, string productCode, Guid userId, Guid? userLocationId = null)
    {
        try
        {
            var handler = _sp.GetRequiredService<InitiateCorporateLoanHandler>();
            Enum.TryParse<InterestRateType>(request.InterestRateType, ignoreCase: true, out var rt);

            var directors = request.Directors.Count > 0
                ? request.Directors.Select(d => new CRMS.Application.LoanApplication.Commands.DirectorInput(
                    FullName: d.FullName,
                    BVN: string.IsNullOrWhiteSpace(d.BVN) ? null : d.BVN,
                    Email: d.Email,
                    PhoneNumber: d.PhoneNumber,
                    ShareholdingPercent: null,
                    IsChairman: d.IsChairman,
                    Designation: d.AffiliateType,
                    DateOfAppointment: d.DateOfAppointment
                  )).ToList()
                : null;

            var signatories = request.Signatories.Count > 0
                ? request.Signatories.Select(s => new CRMS.Application.LoanApplication.Commands.SignatoryInput(
                    FullName: s.FullName,
                    BVN: string.IsNullOrWhiteSpace(s.BVN) ? null : s.BVN,
                    Email: s.Email,
                    PhoneNumber: s.PhoneNumber,
                    Designation: s.Designation,
                    MandateType: s.MandateType ?? "Class A"
                  )).ToList()
                : null;

            var command = new InitiateCorporateLoanCommand(
                LoanProductId: productId,
                ProductCode: productCode,
                AccountNumber: request.AccountNumber,
                RequestedAmount: request.RequestedAmount,
                Currency: "NGN",
                RequestedTenorMonths: request.TenorMonths,
                InterestRatePerAnnum: request.InterestRate,
                InterestRateType: rt == default ? InterestRateType.Flat : rt,
                InitiatedByUserId: userId,
                BranchId: userLocationId,
                Purpose: request.Purpose,
                RegistrationNumberOverride: request.RegistrationNumberOverride,
                IncorporationDateOverride: request.IncorporationDateOverride,
                IndustrySector: request.IndustrySector,
                Directors: directors,
                Signatories: signatories
            );

            var result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess
                ? ApiResponse<Guid>.Ok(result.Data.Id)
                : ApiResponse<Guid>.Fail(result.Error ?? "Failed to create application");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating application");
            return ApiResponse<Guid>.Fail("Failed to create application");
        }
    }

    public async Task<ApiResponse> SubmitApplicationAsync(Guid id, Guid userId)
    {
        try
        {
            SubmitLoanApplicationHandler handler = _sp.GetRequiredService<SubmitLoanApplicationHandler>();
            ApplicationResult result = await handler.Handle(new SubmitLoanApplicationCommand(id, userId), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to submit");
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error submitting application");
            return ApiResponse.Fail("Failed to submit application");
        }
    }

    public async Task<ApiResponse> TransitionWorkflowAsync(Guid workflowInstanceId, LoanApplicationStatus toStatus, WorkflowAction action, string? comments, Guid userId, string userRole)
    {
        try
        {
            TransitionWorkflowHandler handler = _sp.GetRequiredService<TransitionWorkflowHandler>();
            TransitionWorkflowCommand command = new TransitionWorkflowCommand(workflowInstanceId, toStatus, action, userId, userRole, comments);
            ApplicationResult<WorkflowInstanceDto> result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Transition failed");
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error transitioning workflow");
            return ApiResponse.Fail("Workflow transition failed");
        }
    }

    public async Task<List<LoanProduct>> GetLoanProductsAsync()
    {
        try
        {
            GetActiveLoanProductsByTypeHandler handler = _sp.GetRequiredService<GetActiveLoanProductsByTypeHandler>();
            ApplicationResult<List<LoanProductSummaryDto>> result = await handler.Handle(new GetActiveLoanProductsByTypeQuery(LoanProductType.Corporate), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<LoanProduct>();
            }
            return result.Data.Select((LoanProductSummaryDto p) => new LoanProduct
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                MinAmount = p.MinAmount,
                MaxAmount = p.MaxAmount,
                MinTenorMonths = p.MinTenorMonths,
                MaxTenorMonths = p.MaxTenorMonths,
                BaseInterestRate = p.BaseInterestRate,
                IsActive = (p.Status == "Active")
            }).ToList();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching loan products");
            return new List<LoanProduct>();
        }
    }

    public async Task<ApiResponse> GenerateAdvisoryAsync(Guid applicationId, Guid userId)
    {
        try
        {
            GenerateCreditAdvisoryHandler handler = _sp.GetRequiredService<GenerateCreditAdvisoryHandler>();
            GenerateCreditAdvisoryCommand command = new GenerateCreditAdvisoryCommand(applicationId, userId);
            ApplicationResult<CreditAdvisoryDto> result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Advisory generation failed");
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error generating advisory");
            return ApiResponse.Fail("Failed to generate advisory");
        }
    }

    public async Task<ApiResponse<LoanPackResult>> GenerateLoanPackAsync(Guid applicationId, Guid userId, string userName)
    {
        try
        {
            GenerateLoanPackHandler handler = _sp.GetRequiredService<GenerateLoanPackHandler>();
            GenerateLoanPackCommand command = new GenerateLoanPackCommand(applicationId, userId, userName);
            ApplicationResult<LoanPackResultDto> result = await handler.Handle(command, CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return ApiResponse<LoanPackResult>.Fail(result.Error ?? "Loan pack generation failed");
            }
            return ApiResponse<LoanPackResult>.Ok(new LoanPackResult
            {
                LoanPackId = result.Data.LoanPackId,
                FileName = result.Data.FileName,
                StoragePath = result.Data.StoragePath
            });
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error generating loan pack");
            return ApiResponse<LoanPackResult>.Fail("Failed to generate loan pack");
        }
    }

    public async Task<ApiResponse<OfferLetterResult>> GenerateOfferLetterAsync(Guid applicationId, Guid userId, string userName)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.OfferLetter.Commands.GenerateOfferLetterHandler>();
            var command = new CRMS.Application.OfferLetter.Commands.GenerateOfferLetterCommand(applicationId, userId, userName, _bankSettings.BankName, _bankSettings.BranchName);
            var result = await handler.Handle(command, CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
                return ApiResponse<OfferLetterResult>.Fail(result.Error ?? "Offer letter generation failed");
            return ApiResponse<OfferLetterResult>.Ok(new OfferLetterResult
            {
                OfferLetterId = result.Data.OfferLetterId,
                FileName = result.Data.FileName,
                StoragePath = result.Data.StoragePath
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating offer letter");
            return ApiResponse<OfferLetterResult>.Fail("Failed to generate offer letter");
        }
    }

    public async Task<byte[]?> DownloadGeneratedFileAsync(string storagePath)
    {
        try
        {
            var fileStorage = _sp.GetRequiredService<IFileStorageService>();
            return await fileStorage.DownloadAsync(storagePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading generated file {Path}", storagePath);
            return null;
        }
    }

    public async Task<ApiResponse> CastVoteAsync(Guid reviewId, string vote, string? comments, Guid userId)
    {
        try
        {
            CommitteeVote voteValue = Enum.Parse<CommitteeVote>(vote, ignoreCase: true);
            CastVoteHandler handler = _sp.GetRequiredService<CastVoteHandler>();
            CastVoteCommand command = new CastVoteCommand(reviewId, userId, voteValue, comments);
            ApplicationResult<CommitteeReviewDto> result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Vote failed");
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error casting vote");
            return ApiResponse.Fail("Failed to cast vote");
        }
    }

    public async Task<List<CommitteeReviewSummary>> GetMyPendingVotesAsync(Guid userId)
    {
        try
        {
            GetMyPendingVotesHandler handler = _sp.GetRequiredService<GetMyPendingVotesHandler>();
            ApplicationResult<List<CommitteeReviewSummaryDto>> result = await handler.Handle(new GetMyPendingVotesQuery(userId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<CommitteeReviewSummary>();
            }
            
            var loanRepo = _sp.GetRequiredService<ILoanApplicationRepository>();
            var summaries = new List<CommitteeReviewSummary>();

            foreach (var r in result.Data)
            {
                var loan = await loanRepo.GetByIdAsync(r.LoanApplicationId, CancellationToken.None);
                var votesCast = r.ApprovalVotes + r.RejectionVotes;
                var totalMembers = votesCast + r.PendingVotes;

                summaries.Add(new CommitteeReviewSummary
                {
                    ReviewId = r.Id,
                    ApplicationId = r.LoanApplicationId,
                    ApplicationNumber = r.ApplicationNumber,
                    CustomerName = loan?.CustomerName ?? "",
                    CommitteeType = r.CommitteeType,
                    Status = r.Status,
                    DueDate = r.DeadlineAt ?? DateTime.UtcNow.AddDays(7.0),
                    RequestedAmount = loan?.RequestedAmount.Amount ?? 0m,
                    Amount = loan?.RequestedAmount.Amount ?? 0m,
                    VotesCast = votesCast,
                    TotalMembers = totalMembers,
                    HasVoted = r.HasUserVoted,
                    MyVote = r.UserVote,
                    CirculatedAt = r.CirculatedAt
                });
            }
            
            return summaries;
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching pending votes");
            return new List<CommitteeReviewSummary>();
        }
    }

    public async Task<List<WorkflowQueueSummary>> GetQueueSummaryAsync()
    {
        try
        {
            GetQueueSummaryHandler handler = _sp.GetRequiredService<GetQueueSummaryHandler>();
            ApplicationResult<List<WorkflowQueueSummaryDto>> result = await handler.Handle(new GetQueueSummaryQuery(), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<WorkflowQueueSummary>();
            }
            return result.Data.Select((WorkflowQueueSummaryDto q) => new WorkflowQueueSummary
            {
                Stage = q.Role,
                Count = q.TotalCount,
                OverdueCount = q.OverdueCount
            }).ToList();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching queue summary");
            return new List<WorkflowQueueSummary>();
        }
    }

    public async Task<List<WorkflowQueueItem>> GetQueueByRoleAsync(string role)
    {
        try
        {
            GetWorkflowQueueByRoleHandler handler = _sp.GetRequiredService<GetWorkflowQueueByRoleHandler>();
            ApplicationResult<List<WorkflowInstanceSummaryDto>> result = await handler.Handle(new GetWorkflowQueueByRoleQuery(role), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<WorkflowQueueItem>();
            }
            return result.Data.Select((WorkflowInstanceSummaryDto t) => new WorkflowQueueItem
            {
                ApplicationId = t.LoanApplicationId,
                ApplicationNumber = t.ApplicationNumber,
                CustomerName = t.CustomerName,
                Stage = t.CurrentStatus,
                IsOverdue = t.IsOverdue,
                AssignedTo = t.AssignedToUserId?.ToString()
            }).ToList();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching queue by role");
            return new List<WorkflowQueueItem>();
        }
    }

    public async Task<List<OverdueWorkflowItem>> GetOverdueWorkflowsAsync()
    {
        try
        {
            var handler = _sp.GetRequiredService<GetOverdueWorkflowsHandler>();
            var result = await handler.Handle(new GetOverdueWorkflowsQuery(), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<OverdueWorkflowItem>();
            }
            return result.Data
                .Where(t => t.SLADueAt.HasValue)
                .Select(t => new OverdueWorkflowItem
                {
                    ApplicationId = t.LoanApplicationId,
                    ApplicationNumber = t.ApplicationNumber,
                    CustomerName = t.CustomerName,
                    Stage = t.CurrentStageDisplayName ?? t.CurrentStatus,
                    AssignedTo = t.AssignedRole ?? "",
                    SLABreachedAt = t.SLADueAt!.Value
                }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching overdue workflows");
            return new List<OverdueWorkflowItem>();
        }
    }

    public async Task<int> GetOverdueCountAsync()
    {
        try
        {
            var overdueItems = await GetOverdueWorkflowsAsync();
            return overdueItems.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching overdue count");
            return 0;
        }
    }

    public async Task<int> GetMyQueueCountAsync(Guid userId)
    {
        try
        {
            var tasks = await GetMyPendingTasksAsync(userId);
            return tasks.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching my queue count");
            return 0;
        }
    }

    public async Task<int> GetMyPendingVotesCountAsync(Guid userId)
    {
        try
        {
            var votes = await GetMyPendingVotesAsync(userId);
            return votes.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending votes count");
            return 0;
        }
    }

    public async Task<List<CommitteeReviewSummary>> GetCommitteeReviewsByStatusAsync(string? status)
    {
        try
        {
            CommitteeReviewStatus reviewStatus = string.IsNullOrEmpty(status) 
                ? CommitteeReviewStatus.InProgress 
                : Enum.Parse<CommitteeReviewStatus>(status, ignoreCase: true);
            
            var handler = _sp.GetRequiredService<GetCommitteeReviewsByStatusHandler>();
            var result = await handler.Handle(new GetCommitteeReviewsByStatusQuery(reviewStatus), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<CommitteeReviewSummary>();
            }
            
            var loanRepo = _sp.GetRequiredService<ILoanApplicationRepository>();
            var summaries = new List<CommitteeReviewSummary>();
            
            foreach (var r in result.Data)
            {
                var loan = await loanRepo.GetByIdAsync(r.LoanApplicationId, CancellationToken.None);
                var votesCast = r.ApprovalVotes + r.RejectionVotes;
                var totalMembers = votesCast + r.PendingVotes;
                
                summaries.Add(new CommitteeReviewSummary
                {
                    ReviewId = r.Id,
                    ApplicationId = r.LoanApplicationId,
                    ApplicationNumber = r.ApplicationNumber,
                    CustomerName = loan?.CustomerName ?? "",
                    CommitteeType = r.CommitteeType,
                    Status = r.Status,
                    DueDate = r.DeadlineAt ?? DateTime.UtcNow.AddDays(7.0),
                    RequestedAmount = loan?.RequestedAmount.Amount ?? 0m,
                    Amount = loan?.RequestedAmount.Amount ?? 0m,
                    VotesCast = votesCast,
                    TotalMembers = totalMembers > 0 ? totalMembers : 1,
                    FinalDecision = r.FinalDecision
                });
            }
            
            return summaries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching committee reviews by status");
            return new List<CommitteeReviewSummary>();
        }
    }

    public async Task<ReportingMetrics> GetReportingMetricsAsync()
    {
        try
        {
            DashboardSummaryDto dashboard = await _reporting.GetDashboardSummaryAsync();
            LoanFunnelDto funnel = await _reporting.GetLoanFunnelAsync(null, null);
            await _reporting.GetPortfolioSummaryAsync();
            return new ReportingMetrics
            {
                ApplicationsReceived = funnel.Submitted,
                Approved = funnel.Approved,
                ApprovalRate = (int)funnel.ApprovalRate,
                AvgProcessingDays = dashboard.Performance.AverageProcessingTimeDays,
                DisbursedAmount = funnel.DisbursedAmount
            };
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching reporting metrics");
            return new ReportingMetrics();
        }
    }

    public async Task<EnhancedReportingData> GetEnhancedReportingDataAsync(int periodDays)
    {
        try
        {
            var toDate = DateTime.UtcNow;
            var fromDate = toDate.AddDays(-periodDays);
            var prevFrom = fromDate.AddDays(-periodDays);
            var prevTo = fromDate;
            
            var funnel = await _reporting.GetLoanFunnelAsync(fromDate, toDate);
            var prevFunnel = await _reporting.GetLoanFunnelAsync(prevFrom, prevTo);
            var portfolio = await _reporting.GetPortfolioSummaryAsync();
            var slaReport = await _reporting.GetSLAReportAsync(fromDate, toDate);
            var performance = await _reporting.GetPerformanceMetricsAsync(fromDate, toDate);
            
            // Calculate growth
            var applicationsGrowth = prevFunnel.Submitted > 0 
                ? (int)Math.Round((decimal)(funnel.Submitted - prevFunnel.Submitted) / prevFunnel.Submitted * 100)
                : 0;
            var disbursementGrowth = prevFunnel.DisbursedAmount > 0
                ? (int)Math.Round((funnel.DisbursedAmount - prevFunnel.DisbursedAmount) / prevFunnel.DisbursedAmount * 100)
                : 0;
            
            // Build funnel data
            var funnelStages = new List<FunnelStageData>();
            var total = funnel.Submitted > 0 ? funnel.Submitted : 1;
            funnelStages.Add(new FunnelStageData { Stage = "Received", Count = funnel.Submitted, Percentage = 100 });
            funnelStages.Add(new FunnelStageData { Stage = "In Review", Count = funnel.InReview, Percentage = (int)Math.Round((decimal)funnel.InReview / total * 100) });
            funnelStages.Add(new FunnelStageData { Stage = "Approved", Count = funnel.Approved, Percentage = (int)Math.Round((decimal)funnel.Approved / total * 100) });
            funnelStages.Add(new FunnelStageData { Stage = "Disbursed", Count = funnel.Disbursed, Percentage = (int)Math.Round((decimal)funnel.Disbursed / total * 100) });
            funnelStages.Add(new FunnelStageData { Stage = "Rejected", Count = funnel.Rejected, Percentage = (int)Math.Round((decimal)funnel.Rejected / total * 100) });
            
            // Build portfolio data
            var portfolioData = portfolio.LoansByProduct.Select(kvp => new ProductPortfolioData
            {
                ProductName = kvp.Key,
                Count = kvp.Value,
                Amount = portfolio.OutstandingByProduct.GetValueOrDefault(kvp.Key, 0)
            }).ToList();
            
            return new EnhancedReportingData
            {
                ApplicationsReceived = funnel.Submitted,
                ApplicationsGrowth = applicationsGrowth,
                Approved = funnel.Approved,
                ApprovalRate = (int)funnel.ApprovalRate,
                AvgProcessingDays = performance.AverageProcessingTimeDays,
                ProcessingImprovement = (int)(5 - performance.AverageProcessingTimeDays), // Target is 5 days
                DisbursedAmount = funnel.DisbursedAmount,
                DisbursementGrowth = disbursementGrowth,
                Rejected = funnel.Rejected,
                InReview = funnel.InReview,
                FunnelStages = funnelStages,
                PortfolioByProduct = portfolioData,
                SlaCompliance = (int)slaReport.OverallCompliance,
                WithinSla = slaReport.OnTime,
                BreachedSla = slaReport.Breached
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching enhanced reporting data");
            return new EnhancedReportingData();
        }
    }

    public async Task<List<AuditLogSummary>> GetAuditLogsAsync(string? action = null, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            GetRecentAuditLogsHandler handler = _sp.GetRequiredService<GetRecentAuditLogsHandler>();
            ApplicationResult<List<AuditLogSummaryDto>> result = await handler.Handle(new GetRecentAuditLogsQuery(), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<AuditLogSummary>();
            }
            return result.Data.Select((AuditLogSummaryDto l) => new AuditLogSummary
            {
                Id = l.Id,
                Timestamp = l.Timestamp,
                UserName = (l.UserName ?? "System"),
                Action = l.Action,
                EntityType = l.EntityType,
                EntityId = (l.EntityReference ?? ""),
                Details = l.Description,
                IpAddress = ""
            }).ToList();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching audit logs");
            return new List<AuditLogSummary>();
        }
    }

    public async Task<(List<AuditLogSummary> Items, int TotalCount, int TotalPages)> SearchAuditLogsAsync(
        string? actionFilter = null,
        DateTime? from = null,
        DateTime? to = null,
        int pageNumber = 1,
        int pageSize = 20,
        string? searchTerm = null)
    {
        try
        {
            CRMS.Domain.Enums.AuditAction? auditAction = null;
            if (!string.IsNullOrEmpty(actionFilter) &&
                Enum.TryParse<CRMS.Domain.Enums.AuditAction>(actionFilter, true, out var parsed))
            {
                auditAction = parsed;
            }

            var handler = _sp.GetRequiredService<Application.Audit.Queries.SearchAuditLogsHandler>();
            var query = new Application.Audit.Queries.SearchAuditLogsQuery(
                Action: auditAction,
                From: from,
                To: to,
                PageNumber: pageNumber,
                PageSize: pageSize,
                SearchTerm: string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm
            );
            var result = await handler.Handle(query, CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
                return ([], 0, 0);

            var items = result.Data.Items.Select(l => new AuditLogSummary
            {
                Id = l.Id,
                Timestamp = l.Timestamp,
                UserName = l.UserName ?? "System",
                Action = l.Action,
                EntityType = l.EntityType,
                EntityId = l.EntityReference ?? "",
                Details = l.Description,
                IpAddress = ""
            }).ToList();

            return (items, result.Data.TotalCount, result.Data.TotalPages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching audit logs");
            return ([], 0, 0);
        }
    }

    public async Task<List<UserSummary>> GetUsersAsync()
    {
        try
        {
            GetAllUsersHandler handler = _sp.GetRequiredService<GetAllUsersHandler>();
            ApplicationResult<List<UserSummaryDto>> result = await handler.Handle(new GetAllUsersQuery(), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<UserSummary>();
            }
            return result.Data.Select(delegate (UserSummaryDto u)
            {
                string[] array = u.FullName.Split(' ', 2);
                return new UserSummary
                {
                    Id = u.Id,
                    FirstName = ((array.Length != 0) ? array[0] : ""),
                    LastName = ((array.Length > 1) ? array[1] : ""),
                    Email = u.Email,
                    Role = (u.Roles.FirstOrDefault() ?? ""),
                    IsActive = (u.Status == "Active"),
                    LocationId = u.LocationId
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching users");
            return new List<UserSummary>();
        }
    }

    public async Task<ApiResponse> CreateUserAsync(string email, string firstName, string lastName, string? phoneNumber, List<string> roles)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.Identity.Commands.RegisterUserHandler>();
            var userName = email.Split('@')[0];
            var result = await handler.Handle(new CRMS.Application.Identity.Commands.RegisterUserCommand(
                email, userName, "Welcome@1234", firstName, lastName,
                CRMS.Domain.Entities.Identity.UserType.Staff, phoneNumber, null, roles
            ), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to create user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return ApiResponse.Fail("Failed to create user");
        }
    }

    public async Task<ApiResponse> UpdateUserAsync(Guid userId, string firstName, string lastName, string? phoneNumber, Guid? locationId, List<string> roles)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.Identity.Commands.UpdateUserHandler>();
            var result = await handler.Handle(new CRMS.Application.Identity.Commands.UpdateUserCommand(
                userId, firstName, lastName, phoneNumber, locationId, roles
            ), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to update user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {Id}", userId);
            return ApiResponse.Fail("Failed to update user");
        }
    }

    public async Task<ApiResponse> ToggleUserStatusAsync(Guid userId, bool currentlyActive)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.Identity.Commands.ToggleUserStatusHandler>();
            var result = await handler.Handle(new CRMS.Application.Identity.Commands.ToggleUserStatusCommand(userId, currentlyActive), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to update user status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling user status {Id}", userId);
            return ApiResponse.Fail("Failed to update user status");
        }
    }

    // ========== Location Management ==========

    public async Task<List<LocationInfo>> GetAllLocationsAsync(bool includeInactive = false)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.Location.Queries.GetAllLocationsHandler>();
            var result = await handler.Handle(new CRMS.Application.Location.Queries.GetAllLocationsQuery(includeInactive), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
                return [];
            return result.Data.Select(l => new LocationInfo
            {
                Id = l.Id,
                Code = l.Code,
                Name = l.Name,
                Type = l.Type,
                ParentLocationId = l.ParentLocationId,
                ParentLocationName = l.ParentLocationName,
                IsActive = l.IsActive,
                Address = l.Address,
                ManagerName = l.ManagerName,
                ContactPhone = l.ContactPhone,
                ContactEmail = l.ContactEmail,
                SortOrder = l.SortOrder
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching locations");
            return [];
        }
    }

    public async Task<LocationTreeNode?> GetLocationTreeAsync()
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.Location.Queries.GetLocationTreeHandler>();
            var result = await handler.Handle(new CRMS.Application.Location.Queries.GetLocationTreeQuery(), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
                return null;
            return MapTreeNode(result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching location tree");
            return null;
        }
    }

    private static LocationTreeNode MapTreeNode(CRMS.Application.Location.DTOs.LocationTreeNodeDto dto)
    {
        return new LocationTreeNode
        {
            Id = dto.Id,
            Code = dto.Code,
            Name = dto.Name,
            Type = dto.Type,
            IsActive = dto.IsActive,
            ManagerName = dto.ManagerName,
            SortOrder = dto.SortOrder,
            Children = dto.Children.Select(MapTreeNode).ToList()
        };
    }

    public async Task<ApiResponse<Guid>> CreateLocationAsync(string code, string name, string type, Guid? parentId, string? address, string? managerName, string? contactPhone, string? contactEmail, int sortOrder)
    {
        try
        {
            if (!Enum.TryParse<CRMS.Domain.Aggregates.Location.LocationType>(type, out var locationType))
                return ApiResponse<Guid>.Fail($"Invalid location type: {type}");

            var handler = _sp.GetRequiredService<CRMS.Application.Location.Commands.CreateLocationHandler>();
            var result = await handler.Handle(new CRMS.Application.Location.Commands.CreateLocationCommand(
                code, name, locationType, parentId, address, managerName, contactPhone, contactEmail, sortOrder
            ), CancellationToken.None);
            return result.IsSuccess
                ? ApiResponse<Guid>.Ok(result.Data!.Id)
                : ApiResponse<Guid>.Fail(result.Error ?? "Failed to create location");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating location");
            return ApiResponse<Guid>.Fail("Failed to create location");
        }
    }

    public async Task<ApiResponse> UpdateLocationAsync(Guid id, string name, string? address, string? managerName, string? contactPhone, string? contactEmail, int sortOrder)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.Location.Commands.UpdateLocationHandler>();
            var result = await handler.Handle(new CRMS.Application.Location.Commands.UpdateLocationCommand(
                id, name, address, managerName, contactPhone, contactEmail, sortOrder
            ), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to update location");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating location {Id}", id);
            return ApiResponse.Fail("Failed to update location");
        }
    }

    public async Task<ApiResponse> ToggleLocationStatusAsync(Guid id, bool currentlyActive)
    {
        try
        {
            if (currentlyActive)
            {
                var handler = _sp.GetRequiredService<CRMS.Application.Location.Commands.DeactivateLocationHandler>();
                var result = await handler.Handle(new CRMS.Application.Location.Commands.DeactivateLocationCommand(id), CancellationToken.None);
                return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to deactivate location");
            }
            else
            {
                var handler = _sp.GetRequiredService<CRMS.Application.Location.Commands.ActivateLocationHandler>();
                var result = await handler.Handle(new CRMS.Application.Location.Commands.ActivateLocationCommand(id), CancellationToken.None);
                return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to activate location");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling location status {Id}", id);
            return ApiResponse.Fail("Failed to update location status");
        }
    }

    public async Task<List<LocationInfo>> GetLocationsByTypeAsync(string type)
    {
        try
        {
            if (!Enum.TryParse<CRMS.Domain.Aggregates.Location.LocationType>(type, out var locationType))
                return [];

            var handler = _sp.GetRequiredService<CRMS.Application.Location.Queries.GetLocationsByTypeHandler>();
            var result = await handler.Handle(new CRMS.Application.Location.Queries.GetLocationsByTypeQuery(locationType), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
                return [];
            return result.Data.Select(l => new LocationInfo
            {
                Id = l.Id,
                Code = l.Code,
                Name = l.Name,
                Type = l.Type,
                IsActive = l.IsActive
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching locations by type {Type}", type);
            return [];
        }
    }

    public async Task<List<LocationInfo>> GetBranchesAsync()
    {
        return await GetLocationsByTypeAsync("Branch");
    }

    public async Task<List<LocationInfo>> GetAllActiveLocationsForPickerAsync()
    {
        var locations = await GetAllLocationsAsync(false);
        return locations.OrderBy(l => l.Type).ThenBy(l => l.Name).ToList();
    }

    // ========== End Location Management ==========

    public async Task<List<LoanProduct>> GetAllLoanProductsAsync()
    {
        try
        {
            GetAllLoanProductsHandler handler = _sp.GetRequiredService<GetAllLoanProductsHandler>();
            ApplicationResult<List<LoanProductSummaryDto>> result = await handler.Handle(new GetAllLoanProductsQuery(), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<LoanProduct>();
            }
            return result.Data.Select((LoanProductSummaryDto p) => new LoanProduct
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                MinAmount = p.MinAmount,
                MaxAmount = p.MaxAmount,
                MinTenorMonths = p.MinTenorMonths,
                MaxTenorMonths = p.MaxTenorMonths,
                BaseInterestRate = p.BaseInterestRate,
                IsActive = (p.Status == "Active"),
                FineractProductId = p.FineractProductId
            }).ToList();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching all loan products");
            return new List<LoanProduct>();
        }
    }

    public async Task<ApiResponse> CreateLoanProductAsync(string code, string name, string description, decimal minAmount, decimal maxAmount, int minTenorMonths, int maxTenorMonths)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.ProductCatalog.Commands.CreateLoanProductHandler>();
            var result = await handler.Handle(new CRMS.Application.ProductCatalog.Commands.CreateLoanProductCommand(
                code, name, description, CRMS.Domain.Enums.LoanProductType.Corporate,
                minAmount, maxAmount, "NGN", minTenorMonths, maxTenorMonths
            ), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to create product");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating loan product");
            return ApiResponse.Fail("Failed to create product");
        }
    }

    public async Task<ApiResponse> UpdateLoanProductAsync(Guid id, string name, string? description, decimal minAmount, decimal maxAmount, int minTenorMonths, int maxTenorMonths, int? fineractProductId = null)
    {
        try
        {
            var handler = _sp.GetRequiredService<CRMS.Application.ProductCatalog.Commands.UpdateLoanProductHandler>();
            var result = await handler.Handle(new CRMS.Application.ProductCatalog.Commands.UpdateLoanProductCommand(
                id, name, description ?? string.Empty, minAmount, maxAmount, "NGN", minTenorMonths, maxTenorMonths, fineractProductId
            ), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to update product");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating loan product {Id}", id);
            return ApiResponse.Fail("Failed to update product");
        }
    }

    public async Task<ApiResponse> ToggleLoanProductAsync(Guid id, bool currentlyActive)
    {
        try
        {
            if (currentlyActive)
            {
                var handler = _sp.GetRequiredService<CRMS.Application.ProductCatalog.Commands.SuspendLoanProductHandler>();
                var result = await handler.Handle(new CRMS.Application.ProductCatalog.Commands.SuspendLoanProductCommand(id), CancellationToken.None);
                return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to suspend product");
            }
            else
            {
                var handler = _sp.GetRequiredService<CRMS.Application.ProductCatalog.Commands.ActivateLoanProductHandler>();
                var result = await handler.Handle(new CRMS.Application.ProductCatalog.Commands.ActivateLoanProductCommand(id), CancellationToken.None);
                return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to activate product");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling loan product {Id}", id);
            return ApiResponse.Fail("Failed to update product status");
        }
    }

    public async Task<ApiResponse<CollateralResult>> AddCollateralAsync(CRMS.Web.Intranet.Services.AddCollateralRequest request, Guid userId)
    {
        try
        {
            AddCollateralHandler handler = _sp.GetRequiredService<AddCollateralHandler>();
            AddCollateralCommand command = new AddCollateralCommand(Type: Enum.Parse<CollateralType>(request.Type, ignoreCase: true), LoanApplicationId: request.LoanApplicationId, Description: request.Description, CreatedByUserId: userId, AssetIdentifier: request.AssetIdentifier, Location: request.Location, OwnerName: request.OwnerName, OwnershipType: request.OwnershipType);
            ApplicationResult<CollateralDto> result = await handler.Handle(command, CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return ApiResponse<CollateralResult>.Fail(result.Error ?? "Failed to add collateral");
            }
            return ApiResponse<CollateralResult>.Ok(new CollateralResult
            {
                Id = result.Data.Id,
                Reference = result.Data.CollateralReference,
                Status = result.Data.Status
            });
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error adding collateral");
            return ApiResponse<CollateralResult>.Fail("Failed to add collateral");
        }
    }

    public async Task<ApiResponse> SetCollateralValuationAsync(Guid collateralId, decimal marketValue, decimal? forcedSaleValue, decimal? haircutPercentage)
    {
        try
        {
            SetCollateralValuationHandler handler = _sp.GetRequiredService<SetCollateralValuationHandler>();
            SetCollateralValuationCommand command = new SetCollateralValuationCommand(collateralId, marketValue, forcedSaleValue, "NGN", haircutPercentage);
            ApplicationResult<CollateralDto> result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to set valuation");
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error setting collateral valuation");
            return ApiResponse.Fail("Failed to set collateral valuation");
        }
    }

    public async Task<ApiResponse> ApproveCollateralAsync(Guid collateralId, Guid approvedByUserId)
    {
        try
        {
            var handler = _sp.GetRequiredService<ApproveCollateralHandler>();
            var command = new ApproveCollateralCommand(collateralId, approvedByUserId);
            var result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to approve collateral");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving collateral");
            return ApiResponse.Fail("Failed to approve collateral");
        }
    }

    public async Task<ApiResponse<CollateralDocumentResult>> UploadCollateralDocumentAsync(UploadCollateralDocumentRequest request, Guid userId)
    {
        try
        {
            var fileStorage = _sp.GetRequiredService<IFileStorageService>();
            var containerName = $"collateral/{request.CollateralId}/documents";
            var filePath = await fileStorage.UploadAsync(containerName, request.FileName, request.FileContent, request.ContentType);

            var handler = _sp.GetRequiredService<UploadCollateralDocumentHandler>();
            var command = new UploadCollateralDocumentCommand(
                request.CollateralId,
                request.DocumentType,
                request.FileName,
                filePath,
                request.FileSize,
                request.ContentType,
                userId,
                request.Description
            );

            var result = await handler.Handle(command, CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return ApiResponse<CollateralDocumentResult>.Fail(result.Error ?? "Failed to upload collateral document");
            }

            return ApiResponse<CollateralDocumentResult>.Ok(new CollateralDocumentResult
            {
                Id = result.Data.Id,
                FileName = result.Data.FileName,
                DocumentType = result.Data.DocumentType
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading collateral document");
            return ApiResponse<CollateralDocumentResult>.Fail("Failed to upload collateral document");
        }
    }

    public async Task<ApiResponse> DeleteCollateralDocumentAsync(Guid collateralId, Guid documentId)
    {
        try
        {
            // Also delete the file from storage
            var repository = _sp.GetRequiredService<ICollateralRepository>();
            var collateral = await repository.GetByIdWithDetailsAsync(collateralId, CancellationToken.None);
            if (collateral == null)
                return ApiResponse.Fail("Collateral not found");

            var document = collateral.Documents.FirstOrDefault(d => d.Id == documentId);
            if (document != null && !string.IsNullOrEmpty(document.StoragePath))
            {
                var fileStorage = _sp.GetRequiredService<IFileStorageService>();
                await fileStorage.DeleteAsync(document.StoragePath);
            }

            var handler = _sp.GetRequiredService<DeleteCollateralDocumentHandler>();
            var command = new DeleteCollateralDocumentCommand(collateralId, documentId);
            var result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to delete document");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting collateral document");
            return ApiResponse.Fail("Failed to delete collateral document");
        }
    }

    public async Task<ApiResponse> DeleteCollateralAsync(Guid collateralId)
    {
        try
        {
            ICollateralRepository repository = _sp.GetRequiredService<ICollateralRepository>();
            ILoanApplicationRepository loanRepository = _sp.GetRequiredService<ILoanApplicationRepository>();
            IUnitOfWork unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            Collateral collateral = await repository.GetByIdAsync(collateralId, CancellationToken.None);
            if (collateral == null)
            {
                return ApiResponse.Fail("Collateral not found");
            }
            // Application must be in Draft — the LoanOfficer's exclusive edit window
            LoanApplication application = await loanRepository.GetByIdAsync(collateral.LoanApplicationId, CancellationToken.None);
            if (application == null || application.Status != LoanApplicationStatus.Draft)
            {
                return ApiResponse.Fail("Collateral can only be deleted while the application is in Draft status");
            }
            repository.Delete(collateral);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            return ApiResponse.Ok();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error deleting collateral {CollateralId}", collateralId);
            return ApiResponse.Fail("Failed to delete collateral");
        }
    }

    public async Task<ApiResponse<GuarantorResult>> AddGuarantorAsync(AddGuarantorRequest request, Guid userId)
    {
        try
        {
            AddIndividualGuarantorHandler handler = _sp.GetRequiredService<AddIndividualGuarantorHandler>();
            AddIndividualGuarantorCommand command = new AddIndividualGuarantorCommand(GuaranteeType: Enum.Parse<GuaranteeType>(request.GuaranteeType, ignoreCase: true), LoanApplicationId: request.LoanApplicationId, FullName: request.FullName, BVN: request.BVN, CreatedByUserId: userId, RelationshipToApplicant: request.Relationship, Email: request.Email, Phone: request.Phone, Address: request.Address, GuaranteeLimit: request.GuaranteeLimit, Currency: "NGN", IsDirector: request.IsDirector, IsShareholder: request.IsShareholder, ShareholdingPercentage: request.ShareholdingPercentage, DeclaredNetWorth: request.DeclaredNetWorth, Occupation: request.Occupation, EmployerName: request.EmployerName, MonthlyIncome: request.MonthlyIncome);
            ApplicationResult<GuarantorDto> result = await handler.Handle(command, CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return ApiResponse<GuarantorResult>.Fail(result.Error ?? "Failed to add guarantor");
            }
            return ApiResponse<GuarantorResult>.Ok(new GuarantorResult
            {
                Id = result.Data.Id,
                Reference = result.Data.GuarantorReference,
                Status = result.Data.Status
            });
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error adding guarantor");
            return ApiResponse<GuarantorResult>.Fail("Failed to add guarantor");
        }
    }

    public async Task<ApiResponse> DeleteGuarantorAsync(Guid guarantorId)
    {
        try
        {
            IGuarantorRepository repository = _sp.GetRequiredService<IGuarantorRepository>();
            ILoanApplicationRepository loanRepository = _sp.GetRequiredService<ILoanApplicationRepository>();
            IUnitOfWork unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            Guarantor guarantor = await repository.GetByIdAsync(guarantorId, CancellationToken.None);
            if (guarantor == null)
            {
                return ApiResponse.Fail("Guarantor not found");
            }
            // Application must be in Draft — the LoanOfficer's exclusive edit window
            LoanApplication application = await loanRepository.GetByIdAsync(guarantor.LoanApplicationId, CancellationToken.None);
            if (application == null || application.Status != LoanApplicationStatus.Draft)
            {
                return ApiResponse.Fail("Guarantors can only be deleted while the application is in Draft status");
            }
            repository.Delete(guarantor);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            return ApiResponse.Ok();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error deleting guarantor {GuarantorId}", guarantorId);
            return ApiResponse.Fail("Failed to delete guarantor");
        }
    }

    public async Task<ApiResponse> ApproveGuarantorAsync(Guid guarantorId, Guid approvedByUserId)
    {
        try
        {
            var handler = _sp.GetRequiredService<ApproveGuarantorHandler>();
            var command = new ApproveGuarantorCommand(guarantorId, approvedByUserId);
            var result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to approve guarantor");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving guarantor {GuarantorId}", guarantorId);
            return ApiResponse.Fail("Failed to approve guarantor");
        }
    }

    public async Task<ApiResponse> RejectGuarantorAsync(Guid guarantorId, Guid rejectedByUserId, string reason)
    {
        try
        {
            var handler = _sp.GetRequiredService<RejectGuarantorHandler>();
            var command = new RejectGuarantorCommand(guarantorId, rejectedByUserId, reason);
            var result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to reject guarantor");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting guarantor {GuarantorId}", guarantorId);
            return ApiResponse.Fail("Failed to reject guarantor");
        }
    }

    public async Task<ApiResponse<DocumentResult>> UploadDocumentAsync(UploadDocumentRequest request, Guid userId)
    {
        try
        {
            IFileStorageService fileStorage = _sp.GetRequiredService<IFileStorageService>();
            string containerName = $"applications/{request.ApplicationId}/documents";
            string filePath = await fileStorage.UploadAsync(containerName, request.FileName, request.FileContent, request.ContentType);
            UploadDocumentHandler handler = _sp.GetRequiredService<UploadDocumentHandler>();
            UploadDocumentCommand command = new UploadDocumentCommand(Category: Enum.Parse<DocumentCategory>(request.Category, ignoreCase: true), ApplicationId: request.ApplicationId, FileName: request.FileName, FilePath: filePath, FileSize: request.FileSize, ContentType: request.ContentType, UploadedByUserId: userId, Description: request.Description);
            ApplicationResult<LoanApplicationDocumentDto> result = await handler.Handle(command, CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return ApiResponse<DocumentResult>.Fail(result.Error ?? "Failed to upload document");
            }
            return ApiResponse<DocumentResult>.Ok(new DocumentResult
            {
                Id = result.Data.Id,
                FileName = result.Data.FileName,
                Status = result.Data.Status
            });
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error uploading document");
            return ApiResponse<DocumentResult>.Fail("Failed to upload document");
        }
    }

    public async Task<ApiResponse> VerifyDocumentAsync(Guid applicationId, Guid documentId, Guid userId)
    {
        try
        {
            VerifyDocumentHandler handler = _sp.GetRequiredService<VerifyDocumentHandler>();
            VerifyDocumentCommand command = new VerifyDocumentCommand(applicationId, documentId, userId);
            ApplicationResult result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to verify document");
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error verifying document");
            return ApiResponse.Fail("Failed to verify document");
        }
    }

    public async Task<ApiResponse> RejectDocumentAsync(Guid applicationId, Guid documentId, Guid userId, string reason)
    {
        try
        {
            RejectDocumentHandler handler = _sp.GetRequiredService<RejectDocumentHandler>();
            RejectDocumentCommand command = new RejectDocumentCommand(applicationId, documentId, userId, reason);
            ApplicationResult result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to reject document");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting document");
            return ApiResponse.Fail("Failed to reject document");
        }
    }

    public async Task<byte[]?> DownloadDocumentAsync(Guid applicationId, Guid documentId)
    {
        try
        {
            var repo = _sp.GetRequiredService<ILoanApplicationRepository>();
            var app = await repo.GetByIdAsync(applicationId);
            if (app == null) return null;

            var doc = app.Documents.FirstOrDefault(d => d.Id == documentId);
            if (doc == null || string.IsNullOrEmpty(doc.FilePath)) return null;

            var fileStorage = _sp.GetRequiredService<IFileStorageService>();
            return await fileStorage.DownloadAsync(doc.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document {DocumentId} for application {ApplicationId}", documentId, applicationId);
            return null;
        }
    }

    public async Task<ApiResponse<Guid>> CreateFinancialStatementAsync(Guid applicationId, int financialYear, DateTime yearEndDate, string yearType, string currency, string? auditorName, string? auditorFirm, DateTime? auditDate, string? auditOpinion, FinancialStatementModal.BalanceSheetInput bs, FinancialStatementModal.IncomeStatementInput inc, FinancialStatementModal.CashFlowInput cf, Guid userId)
    {
        try
        {
            CreateFinancialStatementHandler createHandler = _sp.GetRequiredService<CreateFinancialStatementHandler>();
            FinancialYearType yearTypeEnum = Enum.Parse<FinancialYearType>(yearType, ignoreCase: true);
            CreateFinancialStatementCommand createCommand = new CreateFinancialStatementCommand(applicationId, financialYear, yearEndDate, yearTypeEnum, InputMethod.ManualEntry, userId, currency, auditorName, auditorFirm, auditDate, auditOpinion);
            ApplicationResult<FinancialStatementDto> createResult = await createHandler.Handle(createCommand, CancellationToken.None);
            if (!createResult.IsSuccess || createResult.Data == null)
            {
                return ApiResponse<Guid>.Fail(createResult.Error ?? "Failed to create financial statement");
            }
            Guid statementId = createResult.Data.Id;
            SetBalanceSheetHandler bsHandler = _sp.GetRequiredService<SetBalanceSheetHandler>();
            SubmitBalanceSheetRequest bsRequest = new SubmitBalanceSheetRequest(bs.CashAndCashEquivalents * 1000m, bs.TradeReceivables * 1000m, bs.Inventory * 1000m, bs.PrepaidExpenses * 1000m, bs.OtherCurrentAssets * 1000m, bs.PropertyPlantEquipment * 1000m, bs.IntangibleAssets * 1000m, bs.LongTermInvestments * 1000m, bs.DeferredTaxAssets * 1000m, bs.OtherNonCurrentAssets * 1000m, bs.TradePayables * 1000m, bs.ShortTermBorrowings * 1000m, bs.CurrentPortionLongTermDebt * 1000m, bs.AccruedExpenses * 1000m, bs.TaxPayable * 1000m, bs.OtherCurrentLiabilities * 1000m, bs.LongTermDebt * 1000m, bs.DeferredTaxLiabilities * 1000m, bs.Provisions * 1000m, bs.OtherNonCurrentLiabilities * 1000m, bs.ShareCapital * 1000m, bs.SharePremium * 1000m, bs.RetainedEarnings * 1000m, bs.OtherReserves * 1000m);
            ApplicationResult<FinancialStatementDto> bsResult = await bsHandler.Handle(new SetBalanceSheetCommand(statementId, bsRequest), CancellationToken.None);
            if (!bsResult.IsSuccess)
            {
                return ApiResponse<Guid>.Fail(bsResult.Error ?? "Failed to save balance sheet");
            }
            SetIncomeStatementHandler incHandler = _sp.GetRequiredService<SetIncomeStatementHandler>();
            SubmitIncomeStatementRequest incRequest = new SubmitIncomeStatementRequest(inc.Revenue * 1000m, inc.OtherOperatingIncome * 1000m, inc.CostOfSales * 1000m, inc.SellingExpenses * 1000m, inc.AdministrativeExpenses * 1000m, inc.DepreciationAmortization * 1000m, inc.OtherOperatingExpenses * 1000m, inc.InterestIncome * 1000m, inc.InterestExpense * 1000m, inc.OtherFinanceCosts * 1000m, inc.IncomeTaxExpense * 1000m, inc.DividendsDeclared * 1000m);
            ApplicationResult<FinancialStatementDto> incResult = await incHandler.Handle(new SetIncomeStatementCommand(statementId, incRequest), CancellationToken.None);
            if (!incResult.IsSuccess)
            {
                return ApiResponse<Guid>.Fail(incResult.Error ?? "Failed to save income statement");
            }
            SetCashFlowStatementHandler cfHandler = _sp.GetRequiredService<SetCashFlowStatementHandler>();
            SubmitCashFlowStatementRequest cfRequest = new SubmitCashFlowStatementRequest(cf.ProfitBeforeTax * 1000m, cf.DepreciationAmortization * 1000m, cf.InterestExpenseAddBack * 1000m, cf.ChangesInWorkingCapital * 1000m, cf.TaxPaid * 1000m, cf.OtherOperatingAdjustments * 1000m, cf.PurchaseOfPPE * 1000m, cf.SaleOfPPE * 1000m, cf.PurchaseOfInvestments * 1000m, cf.SaleOfInvestments * 1000m, cf.InterestReceived * 1000m, cf.DividendsReceived * 1000m, 0m, cf.ProceedsFromBorrowings * 1000m, cf.RepaymentOfBorrowings * 1000m, cf.InterestPaid * 1000m, cf.DividendsPaid * 1000m, cf.ProceedsFromShareIssue * 1000m, 0m, cf.OpeningCashBalance * 1000m);
            ApplicationResult<FinancialStatementDto> cfResult = await cfHandler.Handle(new SetCashFlowStatementCommand(statementId, cfRequest), CancellationToken.None);
            if (!cfResult.IsSuccess)
            {
                return ApiResponse<Guid>.Fail(cfResult.Error ?? "Failed to save cash flow statement");
            }
            SubmitFinancialStatementHandler submitHandler = _sp.GetRequiredService<SubmitFinancialStatementHandler>();
            ApplicationResult<FinancialStatementDto> submitResult = await submitHandler.Handle(new SubmitFinancialStatementCommand(statementId), CancellationToken.None);
            if (!submitResult.IsSuccess)
            {
                return ApiResponse<Guid>.Fail(submitResult.Error ?? "Failed to submit financial statement");
            }
            return ApiResponse<Guid>.Ok(statementId);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error creating financial statement");
            return ApiResponse<Guid>.Fail("Failed to create financial statement: " + ex2.Message);
        }
    }

    public async Task<ApiResponse> ApproveApplicationAsync(Guid applicationId, string? comments, Guid userId, string userRole)
    {
        try
        {
            WorkflowInstanceInfo workflowInstance = await GetWorkflowInstanceByApplicationIdAsync(applicationId);
            if (workflowInstance == null)
            {
                return ApiResponse.Fail("Workflow instance not found for this application");
            }
            string currentStatus = workflowInstance.CurrentStatus;
            if (1 == 0)
            {
            }
            LoanApplicationStatus loanApplicationStatus = currentStatus switch
            {
                "BranchReview" => LoanApplicationStatus.CreditAnalysis,
                "CreditAnalysis" => LoanApplicationStatus.HOReview,
                "HOReview" => LoanApplicationStatus.CommitteeCirculation,
                "CommitteeCirculation" => LoanApplicationStatus.FinalApproval,
                "FinalApproval" => LoanApplicationStatus.Approved,
                _ => LoanApplicationStatus.Approved,
            };
            if (1 == 0)
            {
            }
            LoanApplicationStatus targetStatus = loanApplicationStatus;
            TransitionWorkflowHandler handler = _sp.GetRequiredService<TransitionWorkflowHandler>();
            TransitionWorkflowCommand command = new TransitionWorkflowCommand(workflowInstance.Id, targetStatus, WorkflowAction.Approve, userId, userRole, comments);
            ApplicationResult<WorkflowInstanceDto> result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Approval failed");
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error approving application");
            return ApiResponse.Fail("Failed to approve application");
        }
    }

    public async Task<ApiResponse> ReturnApplicationAsync(Guid applicationId, string comments, Guid userId, string userRole)
    {
        try
        {
            WorkflowInstanceInfo workflowInstance = await GetWorkflowInstanceByApplicationIdAsync(applicationId);
            if (workflowInstance == null)
            {
                return ApiResponse.Fail("Workflow instance not found for this application");
            }
            string currentStatus = workflowInstance.CurrentStatus;
            if (1 == 0)
            {
            }
            LoanApplicationStatus loanApplicationStatus = currentStatus switch
            {
                "BranchReview" => LoanApplicationStatus.Draft,
                "CreditAnalysis" => LoanApplicationStatus.BranchReview,
                "HOReview" => LoanApplicationStatus.CreditAnalysis,
                "CommitteeCirculation" => LoanApplicationStatus.HOReview,
                "FinalApproval" => LoanApplicationStatus.CommitteeCirculation,
                _ => LoanApplicationStatus.Draft,
            };
            if (1 == 0)
            {
            }
            LoanApplicationStatus targetStatus = loanApplicationStatus;
            TransitionWorkflowHandler handler = _sp.GetRequiredService<TransitionWorkflowHandler>();
            TransitionWorkflowCommand command = new TransitionWorkflowCommand(workflowInstance.Id, targetStatus, WorkflowAction.Return, userId, userRole, comments);
            ApplicationResult<WorkflowInstanceDto> result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Return failed");
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error returning application");
            return ApiResponse.Fail("Failed to return application");
        }
    }

    public async Task<ApiResponse> RejectApplicationAsync(Guid applicationId, string comments, Guid userId, string userRole)
    {
        try
        {
            WorkflowInstanceInfo workflowInstance = await GetWorkflowInstanceByApplicationIdAsync(applicationId);
            if (workflowInstance == null)
            {
                return ApiResponse.Fail("Workflow instance not found for this application");
            }
            TransitionWorkflowHandler handler = _sp.GetRequiredService<TransitionWorkflowHandler>();
            TransitionWorkflowCommand command = new TransitionWorkflowCommand(workflowInstance.Id, LoanApplicationStatus.Rejected, WorkflowAction.Reject, userId, userRole, comments);
            ApplicationResult<WorkflowInstanceDto> result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Rejection failed");
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error rejecting application");
            return ApiResponse.Fail("Failed to reject application");
        }
    }

    private async Task<WorkflowInstanceInfo?> GetWorkflowInstanceByApplicationIdAsync(Guid applicationId)
    {
        try
        {
            GetWorkflowByLoanApplicationHandler handler = _sp.GetRequiredService<GetWorkflowByLoanApplicationHandler>();
            ApplicationResult<WorkflowInstanceDto> result = await handler.Handle(new GetWorkflowByLoanApplicationQuery(applicationId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return null;
            }
            return new WorkflowInstanceInfo
            {
                Id = result.Data.Id,
                CurrentStatus = result.Data.CurrentStatus
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<ApiResponse<Guid>> CreateFinancialStatementFromExcelAsync(Guid applicationId, CreateFinancialStatementFromExcelRequest request, Guid userId)
    {
        try
        {
            IFinancialStatementRepository repository = _sp.GetRequiredService<IFinancialStatementRepository>();
            IUnitOfWork unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            string yearType = request.YearType;
            if (1 == 0)
            {
            }
            string text = yearType;
            FinancialYearType financialYearType = ((text == "ManagementAccounts") ? FinancialYearType.ManagementAccounts : ((text == "Projected") ? FinancialYearType.Projected : FinancialYearType.Audited));
            if (1 == 0)
            {
            }
            FinancialYearType yearType2 = financialYearType;
            Result<FinancialStatement> createResult = FinancialStatement.Create(applicationId, request.Year, new DateTime(request.Year, 12, 31), yearType2, InputMethod.ExcelUpload, (userId != Guid.Empty) ? userId : Guid.NewGuid());
            if (createResult.IsFailure)
            {
                return ApiResponse<Guid>.Fail(createResult.Error);
            }
            FinancialStatement statement = createResult.Value;
            BalanceSheetData bs = request.BalanceSheet;
            BalanceSheet balanceSheet = BalanceSheet.Create(statement.Id, bs.CashAndCashEquivalents * 1000m, bs.TradeReceivables * 1000m, bs.Inventory * 1000m, bs.PrepaidExpenses * 1000m, bs.OtherCurrentAssets * 1000m, bs.PropertyPlantEquipment * 1000m, bs.IntangibleAssets * 1000m, bs.LongTermInvestments * 1000m, bs.DeferredTaxAssets * 1000m, bs.OtherNonCurrentAssets * 1000m, bs.TradePayables * 1000m, bs.ShortTermBorrowings * 1000m, bs.CurrentPortionLongTermDebt * 1000m, bs.AccruedExpenses * 1000m, bs.TaxPayable * 1000m, bs.OtherCurrentLiabilities * 1000m, bs.LongTermDebt * 1000m, bs.DeferredTaxLiabilities * 1000m, bs.Provisions * 1000m, bs.OtherNonCurrentLiabilities * 1000m, bs.ShareCapital * 1000m, bs.SharePremium * 1000m, bs.RetainedEarnings * 1000m, bs.OtherReserves * 1000m);
            Result bsResult = statement.SetBalanceSheet(balanceSheet);
            if (bsResult.IsFailure)
            {
                return ApiResponse<Guid>.Fail("Failed to set balance sheet: " + bsResult.Error);
            }
            IncomeStatementData inc = request.IncomeStatement;
            IncomeStatement incomeStatement = IncomeStatement.Create(statement.Id, inc.Revenue * 1000m, inc.OtherOperatingIncome * 1000m, inc.CostOfSales * 1000m, inc.SellingExpenses * 1000m, inc.AdministrativeExpenses * 1000m, inc.DepreciationAmortization * 1000m, inc.OtherOperatingExpenses * 1000m, inc.InterestIncome * 1000m, inc.InterestExpense * 1000m, inc.OtherFinanceCosts * 1000m, inc.IncomeTaxExpense * 1000m, inc.DividendsDeclared * 1000m);
            Result isResult = statement.SetIncomeStatement(incomeStatement);
            if (isResult.IsFailure)
            {
                return ApiResponse<Guid>.Fail("Failed to set income statement: " + isResult.Error);
            }
            if (request.CashFlow != null)
            {
                CashFlowData cf = request.CashFlow;
                CashFlowStatement cashFlowStatement = CashFlowStatement.Create(statement.Id, cf.ProfitBeforeTax * 1000m, cf.DepreciationAmortization * 1000m, cf.InterestExpenseAddBack * 1000m, cf.ChangesInWorkingCapital * 1000m, cf.TaxPaid * 1000m, cf.OtherOperatingAdjustments * 1000m, cf.PurchaseOfPPE * 1000m, cf.SaleOfPPE * 1000m, cf.PurchaseOfInvestments * 1000m, cf.SaleOfInvestments * 1000m, cf.InterestReceived * 1000m, cf.DividendsReceived * 1000m, cf.OtherInvestingActivities * 1000m, cf.ProceedsFromBorrowings * 1000m, cf.RepaymentOfBorrowings * 1000m, cf.InterestPaid * 1000m, cf.DividendsPaid * 1000m, cf.ProceedsFromShareIssue * 1000m, cf.OtherFinancingActivities * 1000m, cf.OpeningCashBalance * 1000m);
                Result cfResult = statement.SetCashFlowStatement(cashFlowStatement);
                if (cfResult.IsFailure)
                {
                    _logger.LogWarning("Failed to set cash flow: {Error}", cfResult.Error);
                }
            }
            await repository.AddAsync(statement, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            _logger.LogInformation("Created financial statement {Id} for application {AppId}, Year {Year} with BS TotalAssets={TotalAssets}", statement.Id, applicationId, request.Year, statement.BalanceSheet?.TotalAssets ?? 0m);
            return ApiResponse<Guid>.Ok(statement.Id);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error creating financial statement from Excel for application {ApplicationId}", applicationId);
            return ApiResponse<Guid>.Fail("Error: " + ex2.Message);
        }
    }

    public async Task<FinancialStatementDetailDto?> GetFinancialStatementByIdAsync(Guid statementId)
    {
        try
        {
            IFinancialStatementRepository repository = _sp.GetRequiredService<IFinancialStatementRepository>();
            FinancialStatement statement = await repository.GetByIdWithDetailsAsync(statementId, CancellationToken.None);
            if (statement == null)
            {
                return null;
            }
            return new FinancialStatementDetailDto
            {
                Id = statement.Id,
                Year = statement.FinancialYear,
                YearEndDate = statement.YearEndDate,
                YearType = statement.YearType.ToString(),
                Status = statement.Status.ToString(),
                BalanceSheet = ((statement.BalanceSheet != null) ? new BalanceSheetDetailDto
                {
                    CashAndCashEquivalents = statement.BalanceSheet.CashAndCashEquivalents,
                    TradeReceivables = statement.BalanceSheet.TradeReceivables,
                    Inventory = statement.BalanceSheet.Inventory,
                    PrepaidExpenses = statement.BalanceSheet.PrepaidExpenses,
                    OtherCurrentAssets = statement.BalanceSheet.OtherCurrentAssets,
                    PropertyPlantEquipment = statement.BalanceSheet.PropertyPlantEquipment,
                    IntangibleAssets = statement.BalanceSheet.IntangibleAssets,
                    LongTermInvestments = statement.BalanceSheet.LongTermInvestments,
                    DeferredTaxAssets = statement.BalanceSheet.DeferredTaxAssets,
                    OtherNonCurrentAssets = statement.BalanceSheet.OtherNonCurrentAssets,
                    TradePayables = statement.BalanceSheet.TradePayables,
                    ShortTermBorrowings = statement.BalanceSheet.ShortTermBorrowings,
                    CurrentPortionLongTermDebt = statement.BalanceSheet.CurrentPortionLongTermDebt,
                    AccruedExpenses = statement.BalanceSheet.AccruedExpenses,
                    TaxPayable = statement.BalanceSheet.TaxPayable,
                    OtherCurrentLiabilities = statement.BalanceSheet.OtherCurrentLiabilities,
                    LongTermDebt = statement.BalanceSheet.LongTermDebt,
                    DeferredTaxLiabilities = statement.BalanceSheet.DeferredTaxLiabilities,
                    Provisions = statement.BalanceSheet.Provisions,
                    OtherNonCurrentLiabilities = statement.BalanceSheet.OtherNonCurrentLiabilities,
                    ShareCapital = statement.BalanceSheet.ShareCapital,
                    SharePremium = statement.BalanceSheet.SharePremium,
                    RetainedEarnings = statement.BalanceSheet.RetainedEarnings,
                    OtherReserves = statement.BalanceSheet.OtherReserves
                } : null),
                IncomeStatement = ((statement.IncomeStatement != null) ? new IncomeStatementDetailDto
                {
                    Revenue = statement.IncomeStatement.Revenue,
                    OtherOperatingIncome = statement.IncomeStatement.OtherOperatingIncome,
                    CostOfSales = statement.IncomeStatement.CostOfSales,
                    SellingExpenses = statement.IncomeStatement.SellingExpenses,
                    AdministrativeExpenses = statement.IncomeStatement.AdministrativeExpenses,
                    DepreciationAmortization = statement.IncomeStatement.DepreciationAmortization,
                    OtherOperatingExpenses = statement.IncomeStatement.OtherOperatingExpenses,
                    InterestIncome = statement.IncomeStatement.InterestIncome,
                    InterestExpense = statement.IncomeStatement.InterestExpense,
                    OtherFinanceCosts = statement.IncomeStatement.OtherFinanceCosts,
                    IncomeTaxExpense = statement.IncomeStatement.IncomeTaxExpense,
                    DividendsDeclared = statement.IncomeStatement.DividendsDeclared
                } : null),
                CashFlow = ((statement.CashFlowStatement != null) ? new CashFlowDetailDto
                {
                    ProfitBeforeTax = statement.CashFlowStatement.ProfitBeforeTax,
                    DepreciationAmortization = statement.CashFlowStatement.DepreciationAmortization,
                    InterestExpenseAddBack = statement.CashFlowStatement.InterestExpenseAddBack,
                    ChangesInWorkingCapital = statement.CashFlowStatement.ChangesInWorkingCapital,
                    TaxPaid = statement.CashFlowStatement.TaxPaid,
                    OtherOperatingAdjustments = statement.CashFlowStatement.OtherOperatingAdjustments,
                    PurchaseOfPPE = statement.CashFlowStatement.PurchaseOfPPE,
                    SaleOfPPE = statement.CashFlowStatement.SaleOfPPE,
                    PurchaseOfInvestments = statement.CashFlowStatement.PurchaseOfInvestments,
                    SaleOfInvestments = statement.CashFlowStatement.SaleOfInvestments,
                    InterestReceived = statement.CashFlowStatement.InterestReceived,
                    DividendsReceived = statement.CashFlowStatement.DividendsReceived,
                    ProceedsFromBorrowings = statement.CashFlowStatement.ProceedsFromBorrowings,
                    RepaymentOfBorrowings = statement.CashFlowStatement.RepaymentOfBorrowings,
                    InterestPaid = statement.CashFlowStatement.InterestPaid,
                    DividendsPaid = statement.CashFlowStatement.DividendsPaid,
                    ProceedsFromShareIssue = statement.CashFlowStatement.ProceedsFromShareIssue,
                    OpeningCashBalance = statement.CashFlowStatement.OpeningCashBalance
                } : null)
            };
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching financial statement {Id}", statementId);
            return null;
        }
    }

    public async Task<ApiResponse<Guid>> UpdateFinancialStatementAsync(Guid statementId, int financialYear, DateTime yearEndDate, string yearType, string currency, string? auditorName, string? auditorFirm, DateTime? auditDate, string? auditOpinion, BalanceSheetInputDto bsInput, IncomeStatementInputDto incInput, CashFlowInputDto cfInput, Guid userId)
    {
        try
        {
            IFinancialStatementRepository repository = _sp.GetRequiredService<IFinancialStatementRepository>();
            IUnitOfWork unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            FinancialStatement statement = await repository.GetByIdWithDetailsAsync(statementId, CancellationToken.None);
            if (statement == null)
            {
                return ApiResponse<Guid>.Fail("Financial statement not found");
            }
            if (statement.Status != FinancialStatementStatus.Draft)
            {
                return ApiResponse<Guid>.Fail("Only draft statements can be edited");
            }
            BalanceSheet balanceSheet = BalanceSheet.Create(statement.Id, bsInput.CashAndCashEquivalents * 1000m, bsInput.TradeReceivables * 1000m, bsInput.Inventory * 1000m, bsInput.PrepaidExpenses * 1000m, bsInput.OtherCurrentAssets * 1000m, bsInput.PropertyPlantEquipment * 1000m, bsInput.IntangibleAssets * 1000m, bsInput.LongTermInvestments * 1000m, bsInput.DeferredTaxAssets * 1000m, bsInput.OtherNonCurrentAssets * 1000m, bsInput.TradePayables * 1000m, bsInput.ShortTermBorrowings * 1000m, bsInput.CurrentPortionLongTermDebt * 1000m, bsInput.AccruedExpenses * 1000m, bsInput.TaxPayable * 1000m, bsInput.OtherCurrentLiabilities * 1000m, bsInput.LongTermDebt * 1000m, bsInput.DeferredTaxLiabilities * 1000m, bsInput.Provisions * 1000m, bsInput.OtherNonCurrentLiabilities * 1000m, bsInput.ShareCapital * 1000m, bsInput.SharePremium * 1000m, bsInput.RetainedEarnings * 1000m, bsInput.OtherReserves * 1000m);
            if (!balanceSheet.IsBalanced())
            {
                return ApiResponse<Guid>.Fail("Balance Sheet does not balance. Total Assets must equal Total Liabilities + Equity.");
            }
            IncomeStatement incomeStatement = IncomeStatement.Create(statement.Id, incInput.Revenue * 1000m, incInput.OtherOperatingIncome * 1000m, incInput.CostOfSales * 1000m, incInput.SellingExpenses * 1000m, incInput.AdministrativeExpenses * 1000m, incInput.DepreciationAmortization * 1000m, incInput.OtherOperatingExpenses * 1000m, incInput.InterestIncome * 1000m, incInput.InterestExpense * 1000m, incInput.OtherFinanceCosts * 1000m, incInput.IncomeTaxExpense * 1000m, incInput.DividendsDeclared * 1000m);
            CashFlowStatement cashFlowStatement = CashFlowStatement.Create(statement.Id, cfInput.ProfitBeforeTax * 1000m, cfInput.DepreciationAmortization * 1000m, cfInput.InterestExpenseAddBack * 1000m, cfInput.ChangesInWorkingCapital * 1000m, cfInput.TaxPaid * 1000m, cfInput.OtherOperatingAdjustments * 1000m, cfInput.PurchaseOfPPE * 1000m, cfInput.SaleOfPPE * 1000m, cfInput.PurchaseOfInvestments * 1000m, cfInput.SaleOfInvestments * 1000m, cfInput.InterestReceived * 1000m, cfInput.DividendsReceived * 1000m, 0m, cfInput.ProceedsFromBorrowings * 1000m, cfInput.RepaymentOfBorrowings * 1000m, cfInput.InterestPaid * 1000m, cfInput.DividendsPaid * 1000m, cfInput.ProceedsFromShareIssue * 1000m, 0m, cfInput.OpeningCashBalance * 1000m);
            Result bsResult = statement.SetBalanceSheet(balanceSheet);
            if (bsResult.IsFailure)
            {
                return ApiResponse<Guid>.Fail("Failed to update balance sheet: " + bsResult.Error);
            }
            Result isResult = statement.SetIncomeStatement(incomeStatement);
            if (isResult.IsFailure)
            {
                return ApiResponse<Guid>.Fail("Failed to update income statement: " + isResult.Error);
            }
            Result cfResult = statement.SetCashFlowStatement(cashFlowStatement);
            if (cfResult.IsFailure)
            {
                _logger.LogWarning("Failed to update cash flow: {Error}", cfResult.Error);
            }
            repository.Update(statement);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            _logger.LogInformation("Updated financial statement {Id}", statementId);
            return ApiResponse<Guid>.Ok(statementId);
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error updating financial statement {Id}", statementId);
            return ApiResponse<Guid>.Fail("Error: " + ex2.Message);
        }
    }

    public async Task<ApiResponse> DeleteFinancialStatementAsync(Guid statementId)
    {
        try
        {
            IFinancialStatementRepository repository = _sp.GetRequiredService<IFinancialStatementRepository>();
            IUnitOfWork unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            FinancialStatement statement = await repository.GetByIdAsync(statementId, CancellationToken.None);
            if (statement == null)
            {
                return ApiResponse.Fail("Financial statement not found");
            }
            if (statement.Status != FinancialStatementStatus.Draft)
            {
                return ApiResponse.Fail("Only draft statements can be deleted. This statement has already been submitted for review.");
            }
            repository.Delete(statement);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            _logger.LogInformation("Deleted financial statement {Id}", statementId);
            return ApiResponse.Ok();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error deleting financial statement {Id}", statementId);
            return ApiResponse.Fail("Error: " + ex2.Message);
        }
    }

    public async Task<ApiResponse> DeleteAllFinancialStatementsAsync(Guid applicationId)
    {
        try
        {
            IFinancialStatementRepository repository = _sp.GetRequiredService<IFinancialStatementRepository>();
            IUnitOfWork unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            IReadOnlyList<FinancialStatement> statements = await repository.GetByLoanApplicationIdAsync(applicationId, CancellationToken.None);
            List<FinancialStatement> nonDraftStatements = statements.Where((FinancialStatement s) => s.Status != FinancialStatementStatus.Draft).ToList();
            if (nonDraftStatements.Any())
            {
                return ApiResponse.Fail($"Cannot delete all statements. {nonDraftStatements.Count} statement(s) have already been submitted for review.");
            }
            foreach (FinancialStatement statement in statements)
            {
                repository.Delete(statement);
            }
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            _logger.LogInformation("Deleted {Count} financial statements for application {AppId}", statements.Count, applicationId);
            return ApiResponse.Ok();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error deleting financial statements for application {AppId}", applicationId);
            return ApiResponse.Fail("Error: " + ex2.Message);
        }
    }

    // ── Scoring Configuration ────────────────────────────────────────────────

    public async Task<ApiResponse<List<ScoringParameterDto>>> GetAllScoringParametersAsync()
    {
        try
        {
            var handler = _sp.GetRequiredService<GetAllScoringParametersHandler>();
            var result = await handler.Handle(new GetAllScoringParametersQuery(), CancellationToken.None);
            return result.IsSuccess
                ? ApiResponse<List<ScoringParameterDto>>.Ok(result.Data!)
                : ApiResponse<List<ScoringParameterDto>>.Fail(result.Error ?? "Failed to load scoring parameters");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading scoring parameters");
            return ApiResponse<List<ScoringParameterDto>>.Fail("Failed to load scoring parameters");
        }
    }

    public async Task<ApiResponse<int>> SeedDefaultScoringParametersAsync(Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<SeedDefaultParametersHandler>();
            var result = await handler.Handle(new SeedDefaultParametersCommand(userId), CancellationToken.None);
            return result.IsSuccess
                ? ApiResponse<int>.Ok(result.Data)
                : ApiResponse<int>.Fail(result.Error ?? "Failed to seed parameters");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding scoring parameters");
            return ApiResponse<int>.Fail("Failed to seed scoring parameters");
        }
    }

    public async Task<ApiResponse> RequestParameterChangeAsync(Guid parameterId, decimal newValue, string reason, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<RequestParameterChangeHandler>();
            var result = await handler.Handle(new RequestParameterChangeCommand(parameterId, newValue, reason, userId), CancellationToken.None);
            return result.IsSuccess
                ? ApiResponse.Ok()
                : ApiResponse.Fail(result.Error ?? "Failed to submit change request");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting parameter change {ParameterId}", parameterId);
            return ApiResponse.Fail("Failed to submit change request");
        }
    }

    public async Task<ApiResponse> ApproveParameterChangeAsync(Guid parameterId, Guid userId, string? notes)
    {
        try
        {
            var handler = _sp.GetRequiredService<ApproveParameterChangeHandler>();
            var result = await handler.Handle(new ApproveParameterChangeCommand(parameterId, userId, notes), CancellationToken.None);
            return result.IsSuccess
                ? ApiResponse.Ok()
                : ApiResponse.Fail(result.Error ?? "Failed to approve change");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving parameter change {ParameterId}", parameterId);
            return ApiResponse.Fail("Failed to approve change");
        }
    }

    public async Task<ApiResponse> RejectParameterChangeAsync(Guid parameterId, Guid userId, string reason)
    {
        try
        {
            var handler = _sp.GetRequiredService<RejectParameterChangeHandler>();
            var result = await handler.Handle(new RejectParameterChangeCommand(parameterId, userId, reason), CancellationToken.None);
            return result.IsSuccess
                ? ApiResponse.Ok()
                : ApiResponse.Fail(result.Error ?? "Failed to reject change");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting parameter change {ParameterId}", parameterId);
            return ApiResponse.Fail("Failed to reject change");
        }
    }

    public async Task<ApiResponse> CancelParameterChangeAsync(Guid parameterId, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<CancelParameterChangeHandler>();
            var result = await handler.Handle(new CancelParameterChangeCommand(parameterId, userId), CancellationToken.None);
            return result.IsSuccess
                ? ApiResponse.Ok()
                : ApiResponse.Fail(result.Error ?? "Failed to cancel change");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling parameter change {ParameterId}", parameterId);
            return ApiResponse.Fail("Failed to cancel change request");
        }
    }

    public async Task<PerformanceReportData> GetPerformanceReportDataAsync(int periodDays)
    {
        try
        {
            var toDate = DateTime.UtcNow;
            var fromDate = toDate.AddDays(-periodDays);
            var prevFrom = fromDate.AddDays(-periodDays);
            var prevTo = fromDate;

            var report = await _reporting.GetPerformanceReportAsync(fromDate, toDate);
            var slaReport = await _reporting.GetSLAReportAsync(fromDate, toDate);
            var prevPerf = await _reporting.GetPerformanceMetricsAsync(prevFrom, prevTo);

            return new PerformanceReportData
            {
                AvgProcessingTimeDays = report.Overall.AverageProcessingTimeDays,
                PrevAvgProcessingTimeDays = prevPerf.AverageProcessingTimeDays,
                SlaComplianceRate = slaReport.OverallCompliance,
                PrevSlaComplianceRate = prevPerf.SLAComplianceRate,
                ApplicationsProcessed = report.Overall.TotalApplicationsThisMonth,
                PrevApplicationsProcessed = prevPerf.TotalApplicationsThisMonth,
                SlaBreaches = slaReport.Breached,
                StagePerformance = slaReport.ByStage.Select(s => new StagePerformanceData
                {
                    Name = s.Stage,
                    TargetHours = s.SLATargetHours,
                    ActualHours = s.AverageTimeHours
                }).ToList(),
                TopPerformers = report.ByUser.Select(u => new PerformerData
                {
                    Name = u.UserName,
                    Initials = GetInitials(u.UserName),
                    ProcessedCount = u.ApplicationsProcessed,
                    AvgHours = u.AverageProcessingTime,
                    SlaCompliance = (int)u.SLACompliance
                }).ToList(),
                TeamPerformance = report.ByStage.Select(s => new TeamPerformanceData
                {
                    Name = s.Stage,
                    TotalProcessed = s.Count,
                    AvgHours = s.AverageTime,
                    SlaCompliance = (int)s.SLACompliance,
                    TrendUp = s.SLACompliance >= 80
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching performance report data");
            return new PerformanceReportData();
        }
    }

    public async Task<CommitteeReportData> GetCommitteeReportDataAsync(int periodDays)
    {
        try
        {
            var toDate = DateTime.UtcNow;
            var fromDate = toDate.AddDays(-periodDays);

            var report = await _reporting.GetCommitteeReportAsync(fromDate, toDate);

            return new CommitteeReportData
            {
                TotalReviews = report.TotalReviews,
                Approved = report.Approved,
                Rejected = report.Rejected,
                ApprovalRate = report.ApprovalRate,
                AvgReviewDays = report.AverageReviewDays,
                ByCommitteeType = report.ByCommitteeType.Select(c => new CommitteeTypeData
                {
                    Name = c.CommitteeType,
                    Reviews = c.Reviews,
                    Approved = c.Approved,
                    Rejected = c.Rejected,
                    ApprovalRate = c.ApprovalRate,
                    AvgReviewDays = c.AverageReviewDays
                }).ToList(),
                MemberStats = report.MemberStats.Select(m => new CommitteeMemberData
                {
                    Name = m.UserName,
                    Initials = GetInitials(m.UserName),
                    VotesCast = m.VotesCast,
                    ApproveVotes = m.ApproveVotes,
                    RejectVotes = m.RejectVotes,
                    ParticipationRate = m.ParticipationRate
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching committee report data");
            return new CommitteeReportData();
        }
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "??";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            : name[..Math.Min(2, name.Length)].ToUpper();
    }

    // ==================== Bureau Report Detail ====================

    public async Task<(BureauReportInfo? Report, List<BureauAccountInfo> Accounts)> GetBureauReportDetailAsync(Guid reportId)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetBureauReportByIdHandler>();
            var result = await handler.Handle(new GetBureauReportByIdQuery(reportId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return (null, new List<BureauAccountInfo>());
            }

            var r = result.Data;
            var report = new BureauReportInfo
            {
                Id = r.Id,
                SubjectName = r.SubjectName,
                SubjectType = r.SubjectType,
                Provider = r.Provider,
                Status = r.Status,
                CreditScore = r.CreditScore,
                Rating = GetScoreGrade(r.CreditScore),
                ActiveLoans = r.ActiveLoans,
                TotalExposure = r.TotalOutstandingBalance,
                TotalOverdue = r.TotalOverdue,
                MaxDelinquencyDays = r.MaxDelinquencyDays,
                HasLegalIssues = r.HasLegalActions,
                ReportDate = r.CompletedAt ?? r.RequestedAt,
                FraudRiskScore = r.FraudRiskScore,
                FraudRecommendation = r.FraudRecommendation,
                PartyId = r.PartyId,
                PartyType = r.PartyType
            };

            var accounts = r.Accounts.Select(a => new BureauAccountInfo
            {
                Id = a.Id,
                AccountNumber = a.AccountNumber,
                CreditorName = a.CreditorName,
                AccountType = a.AccountType,
                Status = a.Status,
                DelinquencyLevel = a.DelinquencyLevel,
                CreditLimit = a.CreditLimit,
                Balance = a.Balance,
                DateOpened = a.DateOpened,
                LastPaymentDate = a.LastPaymentDate
            }).ToList();

            return (report, accounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching bureau report detail {Id}", reportId);
            return (null, new List<BureauAccountInfo>());
        }
    }

    // ==================== Notification Templates ====================

    public async Task<List<NotificationTemplateInfo>> GetNotificationTemplatesAsync()
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Notification.Queries.GetAllNotificationTemplatesHandler>();
            var result = await handler.Handle(new Application.Notification.Queries.GetAllNotificationTemplatesQuery(true), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
                return new List<NotificationTemplateInfo>();

            return result.Data.Select(t => new NotificationTemplateInfo
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                Description = t.Description,
                Type = t.Type,
                Channel = t.Channel,
                Language = t.Language,
                Subject = t.Subject,
                BodyTemplate = t.BodyTemplate,
                BodyHtmlTemplate = t.BodyHtmlTemplate,
                IsActive = t.IsActive,
                Version = t.Version
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching notification templates");
            return new List<NotificationTemplateInfo>();
        }
    }

    public async Task<ApiResponse> CreateNotificationTemplateAsync(CreateTemplateRequest request, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Notification.Commands.CreateNotificationTemplateHandler>();
            var result = await handler.Handle(new Application.Notification.Commands.CreateNotificationTemplateCommand(
                request.Code,
                request.Name,
                request.Description ?? "",
                request.Type,
                request.Channel,
                request.BodyTemplate,
                userId,
                request.Subject,
                request.BodyHtmlTemplate,
                null,
                "en"
            ), CancellationToken.None);

            return result.IsSuccess
                ? ApiResponse.Ok()
                : ApiResponse.Fail(result.Error ?? "Failed to create template");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification template");
            return ApiResponse.Fail("An error occurred while creating the template");
        }
    }

    public async Task<ApiResponse> UpdateNotificationTemplateAsync(Guid id, UpdateTemplateRequest request, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Notification.Commands.UpdateNotificationTemplateHandler>();
            var result = await handler.Handle(new Application.Notification.Commands.UpdateNotificationTemplateCommand(
                id,
                request.Name,
                request.Description ?? "",
                request.BodyTemplate,
                userId,
                request.Subject,
                request.BodyHtmlTemplate,
                null
            ), CancellationToken.None);

            return result.IsSuccess
                ? ApiResponse.Ok()
                : ApiResponse.Fail(result.Error ?? "Failed to update template");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification template {Id}", id);
            return ApiResponse.Fail("An error occurred while updating the template");
        }
    }

    public async Task<ApiResponse> ToggleNotificationTemplateAsync(Guid id, bool activate)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Notification.Commands.ToggleNotificationTemplateHandler>();
            var result = await handler.Handle(new Application.Notification.Commands.ToggleNotificationTemplateCommand(id, activate), CancellationToken.None);

            return result.IsSuccess
                ? ApiResponse.Ok()
                : ApiResponse.Fail(result.Error ?? "Failed to toggle template status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling notification template {Id}", id);
            return ApiResponse.Fail("An error occurred while updating template status");
        }
    }
}
