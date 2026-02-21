using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CRMS.Application.Advisory.Commands;
using CRMS.Application.Advisory.DTOs;
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
using CRMS.Web.Intranet.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRMS.Web.Intranet.Services;

public partial class ApplicationService
{
    private readonly IServiceProvider _sp;

    private readonly IReportingService _reporting;

    private readonly ILogger<ApplicationService> _logger;

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
            ICollateralRepository repository = _sp.GetRequiredService<ICollateralRepository>();
            IUnitOfWork unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            Collateral collateral = await repository.GetByIdAsync(collateralId, CancellationToken.None);
            if (collateral == null)
            {
                return ApiResponse.Fail("Collateral not found");
            }
            if (collateral.Status != CollateralStatus.Proposed && collateral.Status != CollateralStatus.UnderValuation)
            {
                return ApiResponse.Fail("Cannot update collateral that has been valued or approved");
            }
            CollateralType collateralType = Enum.Parse<CollateralType>(request.Type, ignoreCase: true);
            Result updateResult = collateral.UpdateBasicInfo(collateralType, request.Description, request.AssetIdentifier, request.Location, request.OwnerName, request.OwnershipType);
            if (updateResult.IsFailure)
            {
                return ApiResponse.Fail(updateResult.Error);
            }
            repository.Update(collateral);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            return ApiResponse.Ok();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error updating collateral {CollateralId}", collateralId);
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
            IGuarantorRepository repository = _sp.GetRequiredService<IGuarantorRepository>();
            IUnitOfWork unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            Guarantor guarantor = await repository.GetByIdAsync(guarantorId, CancellationToken.None);
            if (guarantor == null)
            {
                return ApiResponse.Fail("Guarantor not found");
            }
            if (guarantor.Status != GuarantorStatus.Proposed && guarantor.Status != GuarantorStatus.PendingVerification)
            {
                return ApiResponse.Fail("Cannot update guarantor that has been verified or approved");
            }
            Result updateResult = guarantor.UpdateBasicInfo(guaranteeType: Enum.Parse<GuaranteeType>(request.GuaranteeType, ignoreCase: true), fullName: request.FullName, bvn: request.BVN, email: request.Email, phone: request.Phone, address: request.Address, relationship: request.Relationship, isDirector: request.IsDirector, isShareholder: request.IsShareholder, shareholdingPercentage: request.ShareholdingPercentage, occupation: request.Occupation, employerName: request.EmployerName, monthlyIncome: request.MonthlyIncome, declaredNetWorth: request.DeclaredNetWorth, guaranteeLimit: request.GuaranteeLimit);
            if (updateResult.IsFailure)
            {
                return ApiResponse.Fail(updateResult.Error);
            }
            repository.Update(guarantor);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            return ApiResponse.Ok();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error updating guarantor {GuarantorId}", guarantorId);
            return ApiResponse.Fail("Failed to update guarantor");
        }
    }

    public ApplicationService(IServiceProvider sp, IReportingService reporting, ILogger<ApplicationService> logger)
    {
        _sp = sp;
        _reporting = reporting;
        _logger = logger;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync()
    {
        try
        {
            DashboardSummaryDto data = await _reporting.GetDashboardSummaryAsync();
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
                ApplicationsByStatus = new List<ApplicationByStatus>(),
                RecentActivities = new List<RecentActivity>()
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
            return result.Data.Select((WorkflowInstanceSummaryDto t) => new PendingTask
            {
                ApplicationId = t.LoanApplicationId,
                ApplicationNumber = t.ApplicationNumber,
                CustomerName = t.CustomerName,
                Stage = t.CurrentStatus,
                RequiredAction = t.CurrentStageDisplayName,
                DueDate = (t.SLADueAt ?? DateTime.UtcNow.AddDays(3.0)),
                Amount = 0m,
                ProductName = ""
            }).ToList();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching pending tasks");
            return new List<PendingTask>();
        }
    }

    public async Task<(List<LoanApplicationSummary> Items, int TotalCount)> GetApplicationsByStatusAsync(string? status)
    {
        try
        {
            if (string.IsNullOrEmpty(status))
            {
                return (Items: new List<LoanApplicationSummary>(), TotalCount: 0);
            }
            LoanApplicationStatus statusEnum = Enum.Parse<LoanApplicationStatus>(status, ignoreCase: true);
            GetLoanApplicationsByStatusHandler handler = _sp.GetRequiredService<GetLoanApplicationsByStatusHandler>();
            ApplicationResult<List<LoanApplicationSummaryDto>> result = await handler.Handle(new GetLoanApplicationsByStatusQuery(statusEnum), CancellationToken.None);
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
                    CompanyName = app.CustomerName
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
                                 Position = (p.Designation ?? ""),
                                 ShareholdingPercentage = p.ShareholdingPercent
                             }).ToList(),
                Signatories = (from p in app.Parties
                               where p.PartyType == "Signatory"
                               select new PartyInfo
                               {
                                   Id = p.Id,
                                   Name = p.FullName,
                                   Position = (p.Designation ?? "")
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
            loanApplicationDetail2.Collaterals = await GetCollateralsForApplicationAsync(id);
            LoanApplicationDetail loanApplicationDetail3 = loanApplicationDetail;
            loanApplicationDetail3.Guarantors = await GetGuarantorsForApplicationAsync(id);
            LoanApplicationDetail loanApplicationDetail4 = loanApplicationDetail;
            loanApplicationDetail4.FinancialStatements = await GetFinancialStatementsForApplicationAsync(id);
            loanApplicationDetail.CreatedAt = app.CreatedAt;
            loanApplicationDetail.LastUpdatedAt = app.ModifiedAt;
            return loanApplicationDetail;
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching application detail for {Id}", id);
            return null;
        }
    }

    private async Task<List<CollateralInfo>> GetCollateralsForApplicationAsync(Guid applicationId)
    {
        try
        {
            GetCollateralByLoanApplicationHandler handler = _sp.GetRequiredService<GetCollateralByLoanApplicationHandler>();
            ApplicationResult<List<CollateralSummaryDto>> result = await handler.Handle(new GetCollateralByLoanApplicationQuery(applicationId), CancellationToken.None);
            if (!result.IsSuccess || result.Data == null)
            {
                return new List<CollateralInfo>();
            }
            return result.Data.Select((CollateralSummaryDto c) => new CollateralInfo
            {
                Id = c.Id,
                Type = c.Type,
                Description = c.Description,
                MarketValue = c.AcceptableValue.GetValueOrDefault(),
                ForcedSaleValue = 0m,
                LoanToValue = 0m,
                Status = c.Status,
                LastValuationDate = null
            }).ToList();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching collaterals for application {Id}", applicationId);
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

    public async Task<ApiResponse<Guid>> CreateApplicationAsync(CreateApplicationRequest request, Guid productId, string productCode, Guid userId)
    {
        try
        {
            InitiateCorporateLoanHandler handler = _sp.GetRequiredService<InitiateCorporateLoanHandler>();
            InterestRateType rt;
            InitiateCorporateLoanCommand command = new InitiateCorporateLoanCommand(InterestRateType: Enum.TryParse<InterestRateType>(request.InterestRateType, ignoreCase: true, out rt) ? rt : InterestRateType.Flat, LoanProductId: productId, ProductCode: productCode, AccountNumber: request.AccountNumber, RequestedAmount: request.RequestedAmount, Currency: "NGN", RequestedTenorMonths: request.TenorMonths, InterestRatePerAnnum: request.InterestRate, InitiatedByUserId: userId, BranchId: null, Purpose: request.Purpose);
            ApplicationResult<LoanApplicationDto> result = await handler.Handle(command, CancellationToken.None);
            return result.IsSuccess ? ApiResponse<Guid>.Ok(result.Data.Id) : ApiResponse<Guid>.Fail(result.Error ?? "Failed to create application");
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error creating application");
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
                MinTenorMonths = 6,
                MaxTenorMonths = 60,
                BaseInterestRate = 15m,
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
            return result.Data.Select((CommitteeReviewSummaryDto r) => new CommitteeReviewSummary
            {
                ReviewId = r.Id,
                ApplicationId = r.LoanApplicationId,
                ApplicationNumber = r.ApplicationNumber,
                CommitteeType = r.CommitteeType,
                Status = r.Status,
                DueDate = (r.DeadlineAt ?? DateTime.UtcNow.AddDays(7.0))
            }).ToList();
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

    public async Task<List<CommitteeReviewSummary>> GetCommitteeReviewsByStatusAsync(string? status)
    {
        try
        {
            if (string.IsNullOrEmpty(status))
            {
                GetCommitteeReviewsByStatusHandler handler = _sp.GetRequiredService<GetCommitteeReviewsByStatusHandler>();
                CommitteeReviewStatus reviewStatus = CommitteeReviewStatus.InProgress;
                ApplicationResult<List<CommitteeReviewSummaryDto>> result = await handler.Handle(new GetCommitteeReviewsByStatusQuery(reviewStatus), CancellationToken.None);
                if (!result.IsSuccess || result.Data == null)
                {
                    return new List<CommitteeReviewSummary>();
                }
                return result.Data.Select((CommitteeReviewSummaryDto r) => new CommitteeReviewSummary
                {
                    ReviewId = r.Id,
                    ApplicationId = r.LoanApplicationId,
                    ApplicationNumber = r.ApplicationNumber,
                    CommitteeType = r.CommitteeType,
                    Status = r.Status,
                    DueDate = (r.DeadlineAt ?? DateTime.UtcNow.AddDays(7.0))
                }).ToList();
            }
            CommitteeReviewStatus statusEnum = Enum.Parse<CommitteeReviewStatus>(status, ignoreCase: true);
            GetCommitteeReviewsByStatusHandler statusHandler = _sp.GetRequiredService<GetCommitteeReviewsByStatusHandler>();
            ApplicationResult<List<CommitteeReviewSummaryDto>> statusResult = await statusHandler.Handle(new GetCommitteeReviewsByStatusQuery(statusEnum), CancellationToken.None);
            if (!statusResult.IsSuccess || statusResult.Data == null)
            {
                return new List<CommitteeReviewSummary>();
            }
            return statusResult.Data.Select((CommitteeReviewSummaryDto r) => new CommitteeReviewSummary
            {
                ReviewId = r.Id,
                ApplicationId = r.LoanApplicationId,
                ApplicationNumber = r.ApplicationNumber,
                CommitteeType = r.CommitteeType,
                Status = r.Status,
                DueDate = (r.DeadlineAt ?? DateTime.UtcNow.AddDays(7.0))
            }).ToList();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching committee reviews by status");
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
                    IsActive = (u.Status == "Active")
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
                MinTenorMonths = 6,
                MaxTenorMonths = 60,
                BaseInterestRate = 15m,
                IsActive = (p.Status == "Active")
            }).ToList();
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            _logger.LogError(ex2, "Error fetching all loan products");
            return new List<LoanProduct>();
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
        await Task.CompletedTask;
        _logger.LogWarning("DownloadDocumentAsync not yet fully implemented - StoragePath needed in DTO");
        return null;
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
            SubmitBalanceSheetRequest bsRequest = new SubmitBalanceSheetRequest(bs.CashAndCashEquivalents, bs.TradeReceivables, bs.Inventory, bs.PrepaidExpenses, bs.OtherCurrentAssets, bs.PropertyPlantEquipment, bs.IntangibleAssets, bs.LongTermInvestments, bs.DeferredTaxAssets, bs.OtherNonCurrentAssets, bs.TradePayables, bs.ShortTermBorrowings, bs.CurrentPortionLongTermDebt, bs.AccruedExpenses, bs.TaxPayable, bs.OtherCurrentLiabilities, bs.LongTermDebt, bs.DeferredTaxLiabilities, bs.Provisions, bs.OtherNonCurrentLiabilities, bs.ShareCapital, bs.SharePremium, bs.RetainedEarnings, bs.OtherReserves);
            ApplicationResult<FinancialStatementDto> bsResult = await bsHandler.Handle(new SetBalanceSheetCommand(statementId, bsRequest), CancellationToken.None);
            if (!bsResult.IsSuccess)
            {
                return ApiResponse<Guid>.Fail(bsResult.Error ?? "Failed to save balance sheet");
            }
            SetIncomeStatementHandler incHandler = _sp.GetRequiredService<SetIncomeStatementHandler>();
            SubmitIncomeStatementRequest incRequest = new SubmitIncomeStatementRequest(inc.Revenue, inc.OtherOperatingIncome, inc.CostOfSales, inc.SellingExpenses, inc.AdministrativeExpenses, inc.DepreciationAmortization, inc.OtherOperatingExpenses, inc.InterestIncome, inc.InterestExpense, inc.OtherFinanceCosts, inc.IncomeTaxExpense, inc.DividendsDeclared);
            ApplicationResult<FinancialStatementDto> incResult = await incHandler.Handle(new SetIncomeStatementCommand(statementId, incRequest), CancellationToken.None);
            if (!incResult.IsSuccess)
            {
                return ApiResponse<Guid>.Fail(incResult.Error ?? "Failed to save income statement");
            }
            SetCashFlowStatementHandler cfHandler = _sp.GetRequiredService<SetCashFlowStatementHandler>();
            SubmitCashFlowStatementRequest cfRequest = new SubmitCashFlowStatementRequest(cf.ProfitBeforeTax, cf.DepreciationAmortization, cf.InterestExpenseAddBack, cf.ChangesInWorkingCapital, cf.TaxPaid, cf.OtherOperatingAdjustments, cf.PurchaseOfPPE, cf.SaleOfPPE, cf.PurchaseOfInvestments, cf.SaleOfInvestments, cf.InterestReceived, cf.DividendsReceived, 0m, cf.ProceedsFromBorrowings, cf.RepaymentOfBorrowings, cf.InterestPaid, cf.DividendsPaid, cf.ProceedsFromShareIssue, 0m, cf.OpeningCashBalance);
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
}
