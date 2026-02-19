using CRMS.Application.LoanApplication.Commands;
using CRMS.Application.LoanApplication.Queries;
using CRMS.Application.ProductCatalog.Queries;
using CRMS.Application.ProductCatalog.Commands;
using CRMS.Application.Workflow.Commands;
using CRMS.Application.Workflow.Queries;
using CRMS.Application.Advisory.Commands;
using CRMS.Application.Committee.Commands;
using CRMS.Application.Committee.Queries;
using CRMS.Application.LoanPack.Commands;
using CRMS.Application.Audit.Queries;
using CRMS.Application.Identity.Commands;
using CRMS.Application.Identity.Queries;
using CRMS.Application.Reporting.Interfaces;
using CRMS.Application.Collateral.Commands;
using CRMS.Application.Guarantor.Commands;
using CRMS.Domain.Enums;
using CRMS.Domain.ValueObjects;
using CRMS.Domain.Interfaces;
using CRMS.Domain.Aggregates.FinancialStatement;
using CRMS.Web.Intranet.Models;
using InterestRateType = CRMS.Domain.ValueObjects.InterestRateType;
using CustomerInfo = CRMS.Web.Intranet.Models.CustomerInfo;
using LoanInfo = CRMS.Web.Intranet.Models.LoanInfo;

namespace CRMS.Web.Intranet.Services;

/// <summary>
/// Service that calls Application layer handlers directly (no HTTP overhead).
/// Used by Server-side Blazor for single-deployment scenarios.
/// </summary>
public partial class ApplicationService
{
    private readonly IServiceProvider _sp;
    private readonly IReportingService _reporting;
    private readonly ILogger<ApplicationService> _logger;

    public ApplicationService(IServiceProvider sp, IReportingService reporting, ILogger<ApplicationService> logger)
    {
        _sp = sp;
        _reporting = reporting;
        _logger = logger;
    }

    #region Dashboard

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        try
        {
            var metrics = await _reporting.GetDashboardMetricsAsync();
            return new DashboardSummary
            {
                TotalApplications = metrics.TotalApplications,
                PendingReview = metrics.PendingReview,
                ApprovedToday = metrics.ApprovedToday,
                TotalDisbursed = metrics.TotalDisbursedAmount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching dashboard summary");
            return new DashboardSummary();
        }
    }

    public async Task<List<PendingTask>> GetMyPendingTasksAsync(Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetMyLoanApplicationsHandler>();
            var result = await handler.Handle(new GetMyLoanApplicationsQuery(userId), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return [];

            return result.Data
                .Where(a => a.Status != "Draft" && a.Status != "Disbursed" && a.Status != "Rejected" && a.Status != "Closed")
                .Select(a => new PendingTask
                {
                    ApplicationId = a.Id,
                    ApplicationNumber = a.ApplicationNumber,
                    CustomerName = a.CustomerName,
                    Stage = a.Status,
                    AssignedDate = a.CreatedAt,
                    IsOverdue = false
                }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending tasks for user {UserId}", userId);
            return [];
        }
    }

    #endregion

    #region Applications

    public async Task<(List<LoanApplicationSummary> Items, int TotalCount)> GetApplicationsByStatusAsync(string? status)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetMyLoanApplicationsHandler>();
            var result = await handler.Handle(new GetMyLoanApplicationsQuery(Guid.Empty), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return ([], 0);

            var items = result.Data
                .Where(a => string.IsNullOrEmpty(status) || a.Status == status)
                .Select(a => new LoanApplicationSummary
                {
                    Id = a.Id,
                    ApplicationNumber = a.ApplicationNumber,
                    CustomerName = a.CustomerName,
                    ProductName = a.ProductCode,
                    Amount = a.RequestedAmount,
                    Status = a.Status,
                    CreatedAt = a.CreatedAt,
                    SubmittedAt = a.SubmittedAt
                }).ToList();

            return (items, items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching applications by status {Status}", status);
            return ([], 0);
        }
    }

    public async Task<List<LoanApplicationSummary>> GetMyApplicationsAsync(Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetMyLoanApplicationsHandler>();
            var result = await handler.Handle(new GetMyLoanApplicationsQuery(userId), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return [];

            return result.Data.Select(a => new LoanApplicationSummary
            {
                Id = a.Id,
                ApplicationNumber = a.ApplicationNumber,
                CustomerName = a.CustomerName,
                ProductName = a.ProductCode,
                Amount = a.RequestedAmount,
                Status = a.Status,
                CreatedAt = a.CreatedAt,
                SubmittedAt = a.SubmittedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching applications for user {UserId}", userId);
            return [];
        }
    }

    public async Task<LoanApplicationDetail?> GetApplicationDetailAsync(Guid id)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetLoanApplicationByIdHandler>();
            var result = await handler.Handle(new GetLoanApplicationByIdQuery(id), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return null;

            var app = result.Data;
            return new LoanApplicationDetail
            {
                Id = app.Id,
                ApplicationNumber = app.ApplicationNumber,
                Status = app.Status,
                Customer = new CustomerInfo
                {
                    AccountNumber = app.AccountNumber,
                    CompanyName = app.CustomerName,
                },
                Loan = new LoanInfo
                {
                    ProductId = app.LoanProductId,
                    ProductName = app.ProductCode,
                    RequestedAmount = app.RequestedAmount,
                    ApprovedAmount = app.ApprovedAmount,
                    InterestRate = app.InterestRatePerAnnum,
                    InterestRateType = app.InterestRateType,
                    TenorMonths = app.RequestedTenorMonths,
                    Purpose = app.Purpose ?? ""
                },
                Directors = app.Parties
                    .Where(p => p.PartyType == "Director")
                    .Select(p => new PartyInfo
                    {
                        Id = p.Id,
                        Name = p.FullName,
                        Position = p.Designation ?? "",
                        ShareholdingPercentage = p.ShareholdingPercent
                    }).ToList(),
                Signatories = app.Parties
                    .Where(p => p.PartyType == "Signatory")
                    .Select(p => new PartyInfo
                    {
                        Id = p.Id,
                        Name = p.FullName,
                        Position = p.Designation ?? ""
                    }).ToList(),
                Documents = app.Documents.Select(d => new DocumentInfo
                {
                    Id = d.Id,
                    Name = d.FileName,
                    Category = d.Category,
                    Status = d.Status,
                    UploadedAt = d.UploadedAt,
                    SizeBytes = d.FileSize
                }).ToList(),
                Collaterals = await GetCollateralsForApplicationAsync(id),
                Guarantors = await GetGuarantorsForApplicationAsync(id),
                FinancialStatements = await GetFinancialStatementsForApplicationAsync(id),
                CreatedAt = app.CreatedAt,
                LastUpdatedAt = app.ModifiedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching application detail for {Id}", id);
            return null;
        }
    }

    private async Task<List<CollateralInfo>> GetCollateralsForApplicationAsync(Guid applicationId)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Collateral.Queries.GetCollateralByLoanApplicationHandler>();
            var result = await handler.Handle(new Application.Collateral.Queries.GetCollateralByLoanApplicationQuery(applicationId), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return [];

            return result.Data.Select(c => new CollateralInfo
            {
                Id = c.Id,
                Type = c.Type,
                Description = c.Description,
                MarketValue = c.AcceptableValue ?? 0,
                ForcedSaleValue = 0,
                LoanToValue = 0,
                Status = c.Status,
                LastValuationDate = null
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching collaterals for application {Id}", applicationId);
            return [];
        }
    }

    private async Task<List<GuarantorInfo>> GetGuarantorsForApplicationAsync(Guid applicationId)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Guarantor.Queries.GetGuarantorsByLoanApplicationHandler>();
            var result = await handler.Handle(new Application.Guarantor.Queries.GetGuarantorsByLoanApplicationQuery(applicationId), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return [];

            return result.Data.Select(g => new GuarantorInfo
            {
                Id = g.Id,
                Name = g.FullName,
                Relationship = g.Type,
                GuaranteeAmount = g.GuaranteeLimit ?? 0,
                Status = g.Status,
                HasBureauReport = g.CreditScore.HasValue
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching guarantors for application {Id}", applicationId);
            return [];
        }
    }

    private async Task<List<FinancialStatementInfo>> GetFinancialStatementsForApplicationAsync(Guid applicationId)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.FinancialAnalysis.Queries.GetFinancialStatementsByLoanApplicationHandler>();
            var result = await handler.Handle(new Application.FinancialAnalysis.Queries.GetFinancialStatementsByLoanApplicationQuery(applicationId), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return [];

            return result.Data.Select(fs => new FinancialStatementInfo
            {
                Id = fs.Id,
                Year = fs.FinancialYear,
                YearEndDate = fs.SubmittedAt,
                YearType = fs.YearType,
                Status = fs.Status,
                TotalAssets = fs.TotalAssets ?? 0,
                Revenue = fs.TotalRevenue ?? 0,
                NetProfit = fs.NetProfit ?? 0,
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching financial statements for application {Id}", applicationId);
            return [];
        }
    }

    public async Task<ApiResponse<Guid>> CreateApplicationAsync(CreateApplicationRequest request, Guid productId, string productCode, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<InitiateCorporateLoanHandler>();
            var command = new InitiateCorporateLoanCommand(
                productId,
                productCode,
                request.AccountNumber,
                request.CompanyName,
                request.CompanyName,
                request.RequestedAmount,
                "NGN",
                request.TenorMonths,
                request.InterestRate,
                InterestRateType.Fixed,
                request.Purpose,
                userId,
                null
            );
            
            var result = await handler.Handle(command, CancellationToken.None);
            
            if (!result.IsSuccess)
                return ApiResponse<Guid>.Fail(result.Error ?? "Failed to create application");

            return ApiResponse<Guid>.Ok(result.Data);
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
            var handler = _sp.GetRequiredService<SubmitLoanApplicationHandler>();
            var result = await handler.Handle(new SubmitLoanApplicationCommand(id, userId), CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to submit application");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting application {Id}", id);
            return ApiResponse.Fail("Failed to submit application");
        }
    }

    #endregion

    #region Products

    public async Task<List<LoanProduct>> GetLoanProductsAsync()
    {
        try
        {
            var handler = _sp.GetRequiredService<GetAllLoanProductsHandler>();
            var result = await handler.Handle(new GetAllLoanProductsQuery(), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return [];

            return result.Data.Select(p => new LoanProduct
            {
                Id = p.Id,
                Code = p.ProductCode,
                Name = p.Name,
                Description = p.Description ?? "",
                MinAmount = p.MinAmount,
                MaxAmount = p.MaxAmount,
                MinTenorMonths = p.MinTenorMonths,
                MaxTenorMonths = p.MaxTenorMonths,
                InterestRate = p.BaseInterestRate,
                IsActive = p.IsActive
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching loan products");
            return [];
        }
    }

    public async Task<List<LoanProduct>> GetAllLoanProductsAsync()
    {
        return await GetLoanProductsAsync();
    }

    #endregion

    #region Collateral Management

    public async Task<ApiResponse<CollateralResult>> AddCollateralAsync(AddCollateralRequest request, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Collateral.Commands.AddCollateralHandler>();
            var collateralType = Enum.Parse<CollateralType>(request.Type, true);
            
            var command = new Application.Collateral.Commands.AddCollateralCommand(
                request.LoanApplicationId,
                collateralType,
                request.Description,
                userId,
                request.AssetIdentifier,
                request.Location,
                request.OwnerName,
                request.OwnershipType
            );
            
            var result = await handler.Handle(command, CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return ApiResponse<CollateralResult>.Fail(result.Error ?? "Failed to add collateral");

            return ApiResponse<CollateralResult>.Ok(new CollateralResult
            {
                Id = result.Data.Id,
                Reference = result.Data.CollateralReference,
                Status = result.Data.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding collateral");
            return ApiResponse<CollateralResult>.Fail("Failed to add collateral");
        }
    }

    public async Task<ApiResponse> SetCollateralValuationAsync(Guid collateralId, decimal marketValue, decimal? forcedSaleValue, decimal? haircutPercentage)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Collateral.Commands.SetCollateralValuationHandler>();
            var command = new Application.Collateral.Commands.SetCollateralValuationCommand(
                collateralId,
                marketValue,
                forcedSaleValue,
                "NGN",
                haircutPercentage
            );
            
            var result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to set valuation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting collateral valuation");
            return ApiResponse.Fail("Failed to set collateral valuation");
        }
    }

    public async Task<ApiResponse> DeleteCollateralAsync(Guid collateralId)
    {
        try
        {
            var repository = _sp.GetRequiredService<ICollateralRepository>();
            var unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            
            var collateral = await repository.GetByIdAsync(collateralId, CancellationToken.None);
            if (collateral == null)
                return ApiResponse.Fail("Collateral not found");

            // Only allow deletion before approval/perfection/rejection
            if (collateral.Status == CollateralStatus.Approved || 
                collateral.Status == CollateralStatus.Perfected || 
                collateral.Status == CollateralStatus.Rejected ||
                collateral.Status == CollateralStatus.Released)
                return ApiResponse.Fail("Cannot delete collateral that has been approved, perfected, or rejected");

            repository.Delete(collateral);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            
            return ApiResponse.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting collateral {CollateralId}", collateralId);
            return ApiResponse.Fail("Failed to delete collateral");
        }
    }

    #endregion

    #region Guarantor Management

    public async Task<ApiResponse<GuarantorResult>> AddGuarantorAsync(AddGuarantorRequest request, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Guarantor.Commands.AddIndividualGuarantorHandler>();
            var guaranteeType = Enum.Parse<GuaranteeType>(request.GuaranteeType, true);
            
            var command = new Application.Guarantor.Commands.AddIndividualGuarantorCommand(
                request.LoanApplicationId,
                request.FullName,
                request.BVN,
                guaranteeType,
                userId,
                request.Relationship,
                request.Email,
                request.Phone,
                request.Address,
                request.GuaranteeLimit,
                "NGN",
                request.IsDirector,
                request.IsShareholder,
                request.ShareholdingPercentage,
                request.DeclaredNetWorth,
                request.Occupation,
                request.EmployerName,
                request.MonthlyIncome
            );
            
            var result = await handler.Handle(command, CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return ApiResponse<GuarantorResult>.Fail(result.Error ?? "Failed to add guarantor");

            return ApiResponse<GuarantorResult>.Ok(new GuarantorResult
            {
                Id = result.Data.Id,
                Reference = result.Data.GuarantorReference,
                Status = result.Data.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding guarantor");
            return ApiResponse<GuarantorResult>.Fail("Failed to add guarantor");
        }
    }

    public async Task<ApiResponse> DeleteGuarantorAsync(Guid guarantorId)
    {
        try
        {
            var repository = _sp.GetRequiredService<IGuarantorRepository>();
            var unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            
            var guarantor = await repository.GetByIdAsync(guarantorId, CancellationToken.None);
            if (guarantor == null)
                return ApiResponse.Fail("Guarantor not found");

            // Only allow deletion before approval
            if (guarantor.Status == GuarantorStatus.Approved || 
                guarantor.Status == GuarantorStatus.Active || 
                guarantor.Status == GuarantorStatus.Rejected)
                return ApiResponse.Fail("Cannot delete guarantor that has been approved, active, or rejected");

            repository.Delete(guarantor);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            
            return ApiResponse.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting guarantor {GuarantorId}", guarantorId);
            return ApiResponse.Fail("Failed to delete guarantor");
        }
    }

    #endregion

    #region Document Management

    public async Task<ApiResponse<DocumentResult>> UploadDocumentAsync(UploadDocumentRequest request, Guid userId)
    {
        try
        {
            var fileStorage = _sp.GetRequiredService<Domain.Interfaces.IFileStorageService>();
            
            var containerName = $"applications/{request.ApplicationId}/documents";
            var filePath = await fileStorage.UploadAsync(
                containerName,
                request.FileName,
                request.FileContent,
                request.ContentType
            );
            
            var handler = _sp.GetRequiredService<Application.LoanApplication.Commands.UploadDocumentHandler>();
            var category = Enum.Parse<DocumentCategory>(request.Category, true);
            
            var command = new Application.LoanApplication.Commands.UploadDocumentCommand(
                request.ApplicationId,
                category,
                request.FileName,
                filePath,
                request.FileSize,
                request.ContentType,
                userId,
                request.Description
            );
            
            var result = await handler.Handle(command, CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return ApiResponse<DocumentResult>.Fail(result.Error ?? "Failed to upload document");

            return ApiResponse<DocumentResult>.Ok(new DocumentResult
            {
                Id = result.Data.Id,
                FileName = result.Data.FileName,
                Status = result.Data.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            return ApiResponse<DocumentResult>.Fail("Failed to upload document");
        }
    }

    public async Task<byte[]?> DownloadDocumentAsync(Guid applicationId, Guid documentId)
    {
        try
        {
            var fileStorage = _sp.GetRequiredService<Domain.Interfaces.IFileStorageService>();
            var containerName = $"applications/{applicationId}/documents";
            return await fileStorage.DownloadAsync(containerName, documentId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document {DocumentId}", documentId);
            return null;
        }
    }

    #endregion

    #region Financial Statements

    public async Task<ApiResponse<Guid>> CreateFinancialStatementAsync(
        Guid applicationId,
        int year,
        string yearType,
        BalanceSheetInputDto balanceSheet,
        IncomeStatementInputDto incomeStatement,
        CashFlowInputDto? cashFlow,
        Guid userId)
    {
        try
        {
            var repository = _sp.GetRequiredService<IFinancialStatementRepository>();
            var unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            
            var yearTypeEnum = Enum.Parse<FinancialYearType>(yearType, true);
            var yearEndDate = new DateTime(year, 12, 31);
            
            var statementResult = FinancialStatement.Create(applicationId, year, yearEndDate, yearTypeEnum, userId);
            if (statementResult.IsFailure)
                return ApiResponse<Guid>.Fail(statementResult.Error);
            
            var statement = statementResult.Value;
            
            // Create balance sheet
            var bs = BalanceSheet.Create(
                statement.Id,
                balanceSheet.CashAndCashEquivalents,
                balanceSheet.TradeReceivables,
                balanceSheet.Inventory,
                balanceSheet.PrepaidExpenses,
                balanceSheet.OtherCurrentAssets,
                balanceSheet.PropertyPlantEquipment,
                balanceSheet.IntangibleAssets,
                balanceSheet.LongTermInvestments,
                balanceSheet.DeferredTaxAssets,
                balanceSheet.OtherNonCurrentAssets,
                balanceSheet.TradePayables,
                balanceSheet.ShortTermBorrowings,
                balanceSheet.CurrentPortionLongTermDebt,
                balanceSheet.AccruedExpenses,
                balanceSheet.TaxPayable,
                balanceSheet.OtherCurrentLiabilities,
                balanceSheet.LongTermDebt,
                balanceSheet.DeferredTaxLiabilities,
                balanceSheet.Provisions,
                balanceSheet.OtherNonCurrentLiabilities,
                balanceSheet.ShareCapital,
                balanceSheet.SharePremium,
                balanceSheet.RetainedEarnings,
                balanceSheet.OtherReserves
            );
            if (bs.IsFailure)
                return ApiResponse<Guid>.Fail(bs.Error);
            statement.SetBalanceSheet(bs.Value);
            
            // Create income statement
            var income = IncomeStatement.Create(
                statement.Id,
                incomeStatement.Revenue,
                incomeStatement.OtherOperatingIncome,
                incomeStatement.CostOfSales,
                incomeStatement.SellingExpenses,
                incomeStatement.AdministrativeExpenses,
                incomeStatement.DepreciationAmortization,
                incomeStatement.OtherOperatingExpenses,
                incomeStatement.InterestIncome,
                incomeStatement.InterestExpense,
                incomeStatement.OtherFinanceCosts,
                incomeStatement.IncomeTaxExpense,
                incomeStatement.DividendsDeclared
            );
            if (income.IsFailure)
                return ApiResponse<Guid>.Fail(income.Error);
            statement.SetIncomeStatement(income.Value);
            
            // Create cash flow if provided
            if (cashFlow != null)
            {
                var cf = CashFlowStatement.Create(
                    statement.Id,
                    cashFlow.ProfitBeforeTax,
                    cashFlow.DepreciationAmortization,
                    cashFlow.InterestExpenseAddBack,
                    cashFlow.ChangesInWorkingCapital,
                    cashFlow.TaxPaid,
                    cashFlow.OtherOperatingAdjustments,
                    cashFlow.PurchaseOfPPE,
                    cashFlow.SaleOfPPE,
                    cashFlow.PurchaseOfInvestments,
                    cashFlow.SaleOfInvestments,
                    cashFlow.InterestReceived,
                    cashFlow.DividendsReceived,
                    cashFlow.ProceedsFromBorrowings,
                    cashFlow.RepaymentOfBorrowings,
                    cashFlow.InterestPaid,
                    cashFlow.DividendsPaid,
                    cashFlow.ProceedsFromShareIssue,
                    cashFlow.OpeningCashBalance
                );
                if (cf.IsFailure)
                    return ApiResponse<Guid>.Fail(cf.Error);
                statement.SetCashFlowStatement(cf.Value);
            }
            
            await repository.AddAsync(statement, CancellationToken.None);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            
            return ApiResponse<Guid>.Ok(statement.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating financial statement");
            return ApiResponse<Guid>.Fail("Failed to create financial statement");
        }
    }

    public async Task<ApiResponse> DeleteFinancialStatementAsync(Guid statementId)
    {
        try
        {
            var repository = _sp.GetRequiredService<IFinancialStatementRepository>();
            var unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            
            var statement = await repository.GetByIdAsync(statementId, CancellationToken.None);
            if (statement == null)
                return ApiResponse.Fail("Financial statement not found");

            if (statement.Status != FinancialStatementStatus.Draft)
                return ApiResponse.Fail("Cannot delete financial statement that is not in draft status");

            repository.Delete(statement);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            
            return ApiResponse.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting financial statement {StatementId}", statementId);
            return ApiResponse.Fail("Failed to delete financial statement");
        }
    }

    public async Task<ApiResponse> DeleteAllFinancialStatementsAsync(Guid applicationId)
    {
        try
        {
            var repository = _sp.GetRequiredService<IFinancialStatementRepository>();
            var unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            
            var statements = await repository.GetByLoanApplicationIdAsync(applicationId, CancellationToken.None);
            
            foreach (var statement in statements.Where(s => s.Status == FinancialStatementStatus.Draft))
            {
                repository.Delete(statement);
            }
            
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            
            return ApiResponse.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all financial statements for application {ApplicationId}", applicationId);
            return ApiResponse.Fail("Failed to delete financial statements");
        }
    }

    #endregion

    #region Workflow

    public async Task<ApiResponse> ApproveApplicationAsync(Guid applicationId, string? comments, Guid userId, string userRole)
    {
        try
        {
            var workflowInstance = await GetWorkflowInstanceByApplicationIdAsync(applicationId);
            if (workflowInstance == null)
                return ApiResponse.Fail("Workflow instance not found");

            var toStatus = workflowInstance.CurrentStatus switch
            {
                "BranchReview" => LoanApplicationStatus.BranchApproved,
                "HOReview" => LoanApplicationStatus.CommitteeCirculation,
                "FinalApproval" => LoanApplicationStatus.Approved,
                _ => LoanApplicationStatus.Approved
            };

            return await TransitionWorkflowAsync(workflowInstance.Id, toStatus, WorkflowAction.Approve, comments, userId, userRole);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving application {ApplicationId}", applicationId);
            return ApiResponse.Fail("Failed to approve application");
        }
    }

    public async Task<ApiResponse> ReturnApplicationAsync(Guid applicationId, string comments, Guid userId, string userRole)
    {
        try
        {
            var workflowInstance = await GetWorkflowInstanceByApplicationIdAsync(applicationId);
            if (workflowInstance == null)
                return ApiResponse.Fail("Workflow instance not found");

            return await TransitionWorkflowAsync(workflowInstance.Id, LoanApplicationStatus.Draft, WorkflowAction.Return, comments, userId, userRole);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning application {ApplicationId}", applicationId);
            return ApiResponse.Fail("Failed to return application");
        }
    }

    public async Task<ApiResponse> RejectApplicationAsync(Guid applicationId, string comments, Guid userId, string userRole)
    {
        try
        {
            var workflowInstance = await GetWorkflowInstanceByApplicationIdAsync(applicationId);
            if (workflowInstance == null)
                return ApiResponse.Fail("Workflow instance not found");

            return await TransitionWorkflowAsync(workflowInstance.Id, LoanApplicationStatus.Rejected, WorkflowAction.Reject, comments, userId, userRole);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting application {ApplicationId}", applicationId);
            return ApiResponse.Fail("Failed to reject application");
        }
    }

    public async Task<ApiResponse> TransitionWorkflowAsync(Guid workflowInstanceId, LoanApplicationStatus toStatus, WorkflowAction action, string? comments, Guid userId, string userRole)
    {
        try
        {
            var handler = _sp.GetRequiredService<TransitionWorkflowHandler>();
            var command = new TransitionWorkflowCommand(workflowInstanceId, toStatus, action, comments, userId, userRole);
            var result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to transition workflow");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transitioning workflow {WorkflowInstanceId}", workflowInstanceId);
            return ApiResponse.Fail("Failed to transition workflow");
        }
    }

    private async Task<WorkflowInstanceInfo?> GetWorkflowInstanceByApplicationIdAsync(Guid applicationId)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetWorkflowInstanceByApplicationHandler>();
            var result = await handler.Handle(new GetWorkflowInstanceByApplicationQuery(applicationId), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return null;

            return new WorkflowInstanceInfo
            {
                Id = result.Data.Id,
                CurrentStatus = result.Data.CurrentStatus
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workflow instance for application {ApplicationId}", applicationId);
            return null;
        }
    }

    #endregion

    #region Committee

    public async Task<ApiResponse> CastVoteAsync(Guid reviewId, string vote, string? comments, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<CastVoteHandler>();
            var voteDecision = Enum.Parse<CommitteeVoteDecision>(vote, true);
            var command = new CastVoteCommand(reviewId, userId, voteDecision, comments);
            var result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to cast vote");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error casting vote for review {ReviewId}", reviewId);
            return ApiResponse.Fail("Failed to cast vote");
        }
    }

    public async Task<List<CommitteeReviewSummary>> GetMyPendingVotesAsync(Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetMyPendingVotesHandler>();
            var result = await handler.Handle(new GetMyPendingVotesQuery(userId), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return [];

            return result.Data.Select(r => new CommitteeReviewSummary
            {
                ReviewId = r.ReviewId,
                ApplicationId = r.ApplicationId,
                ApplicationNumber = r.ApplicationNumber,
                CustomerName = r.CustomerName,
                RequestedAmount = r.RequestedAmount,
                CommitteeType = r.CommitteeType,
                Status = r.Status,
                CirculatedAt = r.CirculatedAt,
                DueDate = r.DueDate,
                HasVoted = r.HasVoted,
                MyVote = r.MyVote
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending votes for user {UserId}", userId);
            return [];
        }
    }

    #endregion

    #region Advisory & Loan Pack

    public async Task<ApiResponse> GenerateAdvisoryAsync(Guid applicationId, Guid userId)
    {
        try
        {
            var handler = _sp.GetRequiredService<GenerateCreditAdvisoryHandler>();
            var command = new GenerateCreditAdvisoryCommand(applicationId, userId);
            var result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse.Ok() : ApiResponse.Fail(result.Error ?? "Failed to generate advisory");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating advisory for application {ApplicationId}", applicationId);
            return ApiResponse.Fail("Failed to generate advisory");
        }
    }

    public async Task<ApiResponse<LoanPackResult>> GenerateLoanPackAsync(Guid applicationId, Guid userId, string userName)
    {
        try
        {
            var handler = _sp.GetRequiredService<GenerateLoanPackHandler>();
            var command = new GenerateLoanPackCommand(applicationId, userId, userName);
            var result = await handler.Handle(command, CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return ApiResponse<LoanPackResult>.Fail(result.Error ?? "Failed to generate loan pack");

            return ApiResponse<LoanPackResult>.Ok(new LoanPackResult
            {
                LoanPackId = result.Data.LoanPackId,
                FileName = result.Data.FileName,
                StoragePath = result.Data.StoragePath
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating loan pack for application {ApplicationId}", applicationId);
            return ApiResponse<LoanPackResult>.Fail("Failed to generate loan pack");
        }
    }

    #endregion

    #region Reporting & Audit

    public async Task<ReportingMetrics> GetReportingMetricsAsync()
    {
        try
        {
            var metrics = await _reporting.GetDashboardMetricsAsync();
            return new ReportingMetrics
            {
                ApplicationsReceived = metrics.TotalApplications,
                Approved = metrics.ApprovedToday,
                ApprovalRate = metrics.TotalApplications > 0 ? (int)((double)metrics.ApprovedToday / metrics.TotalApplications * 100) : 0,
                AvgProcessingDays = 5,
                DisbursedAmount = metrics.TotalDisbursedAmount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching reporting metrics");
            return new ReportingMetrics();
        }
    }

    public async Task<List<AuditLogSummary>> GetAuditLogsAsync(string? action = null, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var handler = _sp.GetRequiredService<GetRecentAuditLogsHandler>();
            var result = await handler.Handle(new GetRecentAuditLogsQuery(100), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return [];

            return result.Data.Select(a => new AuditLogSummary
            {
                Id = a.Id,
                Timestamp = a.Timestamp,
                UserName = a.UserName,
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Details = a.Details ?? "",
                IpAddress = a.IpAddress ?? ""
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching audit logs");
            return [];
        }
    }

    #endregion

    #region Users

    public async Task<List<UserSummary>> GetUsersAsync()
    {
        try
        {
            var handler = _sp.GetRequiredService<GetAllUsersHandler>();
            var result = await handler.Handle(new GetAllUsersQuery(), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return [];

            return result.Data.Select(u => new UserSummary
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                Role = u.PrimaryRole,
                IsActive = u.IsActive
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users");
            return [];
        }
    }

    #endregion
}

// All DTOs are defined in ApplicationModels.cs
