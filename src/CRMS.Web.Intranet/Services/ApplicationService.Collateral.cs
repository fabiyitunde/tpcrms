using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Web.Intranet.Models;

namespace CRMS.Web.Intranet.Services;

public partial class ApplicationService
{
    public async Task<CollateralDetailDto?> GetCollateralDetailAsync(Guid collateralId)
    {
        try
        {
            var handler = _sp.GetRequiredService<Application.Collateral.Queries.GetCollateralByIdHandler>();
            var result = await handler.Handle(new Application.Collateral.Queries.GetCollateralByIdQuery(collateralId), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return null;

            var c = result.Data;
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
                RejectionReason = c.RejectionReason
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching collateral detail for {CollateralId}", collateralId);
            return null;
        }
    }

    public async Task<ApiResponse> UpdateCollateralAsync(Guid collateralId, UpdateCollateralRequest request)
    {
        try
        {
            var repository = _sp.GetRequiredService<ICollateralRepository>();
            var unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            
            var collateral = await repository.GetByIdAsync(collateralId, CancellationToken.None);
            if (collateral == null)
                return ApiResponse.Fail("Collateral not found");

            if (collateral.Status != CollateralStatus.Proposed && collateral.Status != CollateralStatus.UnderValuation)
                return ApiResponse.Fail("Cannot update collateral that has been valued or approved");

            var collateralType = Enum.Parse<CollateralType>(request.Type, true);
            var updateResult = collateral.UpdateBasicInfo(collateralType, request.Description, request.AssetIdentifier, 
                request.Location, request.OwnerName, request.OwnershipType);

            if (updateResult.IsFailure)
                return ApiResponse.Fail(updateResult.Error);

            repository.Update(collateral);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            
            return ApiResponse.Ok();
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
            var handler = _sp.GetRequiredService<Application.Guarantor.Queries.GetGuarantorByIdHandler>();
            var result = await handler.Handle(new Application.Guarantor.Queries.GetGuarantorByIdQuery(guarantorId), CancellationToken.None);
            
            if (!result.IsSuccess || result.Data == null)
                return null;

            var g = result.Data;
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
            _logger.LogError(ex, "Error fetching guarantor detail for {GuarantorId}", guarantorId);
            return null;
        }
    }

    public async Task<ApiResponse> UpdateGuarantorAsync(Guid guarantorId, UpdateGuarantorRequest request)
    {
        try
        {
            var repository = _sp.GetRequiredService<IGuarantorRepository>();
            var unitOfWork = _sp.GetRequiredService<IUnitOfWork>();
            
            var guarantor = await repository.GetByIdAsync(guarantorId, CancellationToken.None);
            if (guarantor == null)
                return ApiResponse.Fail("Guarantor not found");

            if (guarantor.Status != GuarantorStatus.Proposed && guarantor.Status != GuarantorStatus.PendingVerification)
                return ApiResponse.Fail("Cannot update guarantor that has been verified or approved");

            var guaranteeType = Enum.Parse<GuaranteeType>(request.GuaranteeType, true);
            var updateResult = guarantor.UpdateBasicInfo(request.FullName, request.BVN, request.Email, request.Phone, 
                request.Address, request.Relationship, guaranteeType, request.IsDirector, request.IsShareholder,
                request.ShareholdingPercentage, request.Occupation, request.EmployerName, 
                request.MonthlyIncome, request.DeclaredNetWorth, request.GuaranteeLimit);

            if (updateResult.IsFailure)
                return ApiResponse.Fail(updateResult.Error);

            repository.Update(guarantor);
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            
            return ApiResponse.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating guarantor {GuarantorId}", guarantorId);
            return ApiResponse.Fail("Failed to update guarantor");
        }
    }
}
