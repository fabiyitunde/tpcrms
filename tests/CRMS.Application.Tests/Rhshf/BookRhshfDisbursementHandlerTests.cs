using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.ProductCatalog;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Domain.ValueObjects;

namespace CRMS.Application.Tests.Rhshf;

public class BookRhshfDisbursementHandlerTests
{
    private const decimal TotalEopValue = 51_500_000.00m;
    private const string SupplierAccountNumber = "0987654321";
    private const string SupplierName = "Agro Inputs Ltd";

    private static RhshfCreditProfile MakeProfileAtDisbursement()
    {
        var result = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: TotalEopValue, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null);
        var profile = result.Value;

        foreach (var stage in new[]
        {
            RhshfProfilingStage.CompanyVerification, RhshfProfilingStage.CreditBureauCheck,
            RhshfProfilingStage.EopReview, RhshfProfilingStage.SupportingDocuments, RhshfProfilingStage.ReviewAndSubmit,
        })
        {
            profile.AdvanceStage(stage);
        }

        profile.Appraise(Guid.NewGuid(), RhshfAppraisalOutcome.Proceed, null);
        profile.ReviewRisk(Guid.NewGuid(), RhshfRiskReviewOutcome.Cleared, null);
        profile.AdvanceToRatification();
        profile.Ratify(Guid.NewGuid(), RhshfRatificationOutcome.Ratified, TotalEopValue, null, null, []);

        var offer = RhshfOffer.Create(profile.Id, profile.CurrentCycleNumber, "path/offer-cycle1.pdf").Value;
        offer.AddDocument("signed.pdf", "application/pdf", "path/signed.pdf", 1024);
        offer.Accept(null);
        profile.AdvanceToLegalClearance();
        profile.AdvanceToDisbursement();

        return profile;
    }

    private static LoanProduct MakeRhshfProduct(int fineractProductId)
    {
        var product = LoanProduct.Create(
            "RHSHF-DRY", "RH-SHF Dry Season Input Financing", "desc", LoanProductType.Rhshf,
            Money.Create(100_000), Money.Create(100_000_000), 3, 9, 12.5m).Value;
        product.ChangeSegment(LoanProductType.Rhshf);
        product.Update("RH-SHF Dry Season Input Financing", "desc", Money.Create(100_000), Money.Create(100_000_000), 3, 9, 12.5m, fineractProductId);
        product.Activate();
        return product;
    }

    private static FineractLoanProduct MakeFineractProduct(int id, decimal annualRate, int defaultRepayments) => new(
        Id: id, Name: "RHSHF", ShortName: "RHSHF", Description: null, CurrencyCode: "NGN", CurrencySymbol: "N",
        MinPrincipal: 100_000, DefaultPrincipal: 1_000_000, MaxPrincipal: 100_000_000,
        DefaultInterestRatePerPeriod: annualRate, MinInterestRatePerPeriod: null, MaxInterestRatePerPeriod: null,
        AnnualInterestRate: annualRate, InterestRateFrequencyType: "Per year",
        DefaultNumberOfRepayments: defaultRepayments, MinNumberOfRepayments: null, MaxNumberOfRepayments: null,
        RepaymentEvery: 1, RepaymentFrequencyType: "Months", AmortizationType: "Equal installments",
        InterestType: "Declining Balance", TransactionProcessingStrategyId: 1,
        StartDate: null, CloseDate: null, IsActive: true);

    [Fact]
    public async Task Book_Succeeds_CompletesCase_UsesLiveFineractRateAndTenor()
    {
        var profile = MakeProfileAtDisbursement();
        var product = MakeRhshfProduct(77);
        var fineract = new FakeFineractDirectService
        {
            ClientId = 555,
            Products = [MakeFineractProduct(77, 9.5m, 6)],
            BookingResult = new FineractBookingResult(9001, "LN-009001", true, true, true, "Active"),
        };
        var handler = new BookRhshfDisbursementHandler(
            new FakeProfileRepository(profile), new FakeLoanProductRepository(product), fineract, new FakeUnitOfWork());

        var result = await handler.Handle(new BookRhshfDisbursementCommand(profile.Reference, Guid.NewGuid(), SupplierAccountNumber, SupplierName));

        Assert.True(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Completed, profile.InternalStage);
        Assert.Equal(RhshfCaseStatus.Approved, profile.Status);
        Assert.NotNull(fineract.LastBookingRequest);
        Assert.Equal(9.5m, fineract.LastBookingRequest!.InterestRatePerAnnum);
        Assert.Equal(6, fineract.LastBookingRequest.TenorMonths);
        Assert.False(fineract.LastBookingRequest.DisburseToSavings);
        Assert.Equal("0123456789", fineract.LastBookingRequest.RepaymentAccountNumber);
        Assert.Equal(TotalEopValue, fineract.LastBookingRequest.Principal);
    }

    [Fact]
    public async Task Book_WithoutSupplierAccount_Fails_DoesNotCallFineract()
    {
        var profile = MakeProfileAtDisbursement();
        var fineract = new FakeFineractDirectService { ClientId = 555, Products = [MakeFineractProduct(77, 9.5m, 6)] };
        var handler = new BookRhshfDisbursementHandler(
            new FakeProfileRepository(profile), new FakeLoanProductRepository(MakeRhshfProduct(77)), fineract, new FakeUnitOfWork());

        var result = await handler.Handle(new BookRhshfDisbursementCommand(profile.Reference, Guid.NewGuid(), "  ", null));

        Assert.False(result.IsSuccess);
        Assert.Null(fineract.LastBookingRequest);
        Assert.Empty(profile.Disbursements);
    }

    [Fact]
    public async Task Book_NoActiveRhshfProductConfigured_Fails_DoesNotCallFineract()
    {
        var profile = MakeProfileAtDisbursement();
        var fineract = new FakeFineractDirectService { ClientId = 555 };
        var handler = new BookRhshfDisbursementHandler(
            new FakeProfileRepository(profile), new FakeLoanProductRepository(null), fineract, new FakeUnitOfWork());

        var result = await handler.Handle(new BookRhshfDisbursementCommand(profile.Reference, Guid.NewGuid(), SupplierAccountNumber, SupplierName));

        Assert.False(result.IsSuccess);
        Assert.Null(fineract.LastBookingRequest);
        Assert.Empty(profile.Disbursements);
    }

    [Fact]
    public async Task Book_FineractBookingFails_RecordsFailedAttempt_StaysAtDisbursement_Retryable()
    {
        var profile = MakeProfileAtDisbursement();
        var fineract = new FakeFineractDirectService
        {
            ClientId = 555,
            Products = [MakeFineractProduct(77, 9.5m, 6)],
            BookingFailureMessage = "Fineract unavailable",
        };
        var handler = new BookRhshfDisbursementHandler(
            new FakeProfileRepository(profile), new FakeLoanProductRepository(MakeRhshfProduct(77)), fineract, new FakeUnitOfWork());

        var result = await handler.Handle(new BookRhshfDisbursementCommand(profile.Reference, Guid.NewGuid(), SupplierAccountNumber, SupplierName));

        Assert.False(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Disbursement, profile.InternalStage);
        Assert.Equal(RhshfCaseStatus.UnderReview, profile.Status);
        Assert.Single(profile.Disbursements);
        Assert.Equal(RhshfDisbursementStatus.Failed, profile.Disbursements.Single().Status);
    }

    [Fact]
    public async Task Book_ClientResolutionFails_RecordsFailedAttempt_Retryable()
    {
        var profile = MakeProfileAtDisbursement();
        var fineract = new FakeFineractDirectService { ClientResolutionFailureMessage = "Account not found" };
        var handler = new BookRhshfDisbursementHandler(
            new FakeProfileRepository(profile), new FakeLoanProductRepository(MakeRhshfProduct(77)), fineract, new FakeUnitOfWork());

        var result = await handler.Handle(new BookRhshfDisbursementCommand(profile.Reference, Guid.NewGuid(), SupplierAccountNumber, SupplierName));

        Assert.False(result.IsSuccess);
        Assert.Equal(RhshfInternalStage.Disbursement, profile.InternalStage);
        Assert.Single(profile.Disbursements);
        Assert.Equal(RhshfDisbursementStatus.Failed, profile.Disbursements.Single().Status);
    }

    private class FakeProfileRepository : IRhshfCreditProfileRepository
    {
        private readonly RhshfCreditProfile? _profile;
        public FakeProfileRepository(RhshfCreditProfile? profile) => _profile = profile;
        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_profile?.Id == id ? _profile : null);
        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult(_profile?.Reference == reference ? _profile : null);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
            => Task.FromResult(_profile?.SubmissionId == submissionId ? _profile : null);
        public Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>(
                _profile is not null && _profile.InternalStage == stage ? [_profile] : []);
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeLoanProductRepository : ILoanProductRepository
    {
        private readonly LoanProduct? _product;
        public FakeLoanProductRepository(LoanProduct? product) => _product = product;
        public Task<LoanProduct?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_product?.Id == id ? _product : null);
        public Task<LoanProduct?> GetByCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LoanProduct>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LoanProduct>> GetByStatusAsync(ProductStatus status, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LoanProduct>> GetByTypeAsync(LoanProductType type, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LoanProduct>> GetActiveByTypeAsync(LoanProductType type, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<LoanProduct?> GetActiveNampProductAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<LoanProduct?> GetActiveRhshfProductAsync(CancellationToken ct = default) => Task.FromResult(_product);
        public Task<bool> ExistsAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(LoanProduct product, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(LoanProduct product, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddChecklistTemplateItemAsync(DisbursementChecklistTemplate item, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeFineractDirectService : IFineractDirectService
    {
        public long ClientId { get; set; }
        public IReadOnlyList<FineractLoanProduct> Products { get; set; } = [];
        public FineractBookingResult? BookingResult { get; set; }
        public string? BookingFailureMessage { get; set; }
        public string? ClientResolutionFailureMessage { get; set; }
        public FineractLoanBookingRequest? LastBookingRequest { get; private set; }

        public Task<Result<NampBoaAccountInfo>> GetNampBoaAccountAsync(string boaAccountNumber, CancellationToken ct = default)
            => Task.FromResult(ClientResolutionFailureMessage is not null
                ? Result.Failure<NampBoaAccountInfo>(ClientResolutionFailureMessage)
                : Result.Success(new NampBoaAccountInfo(ClientId, "Active", 1, "SAV-001")));

        public Task<Result<IReadOnlyList<FineractLoanProduct>>> GetLoanProductsAsync(bool activeOnly = true, CancellationToken ct = default)
            => Task.FromResult(Result.Success(Products));

        public Task<Result<FineractBookingResult>> BookApprovedLoanAsync(FineractLoanBookingRequest request, CancellationToken ct = default)
        {
            LastBookingRequest = request;
            if (BookingFailureMessage is not null)
                return Task.FromResult(Result.Failure<FineractBookingResult>(BookingFailureMessage));
            return Task.FromResult(Result.Success(BookingResult!));
        }

        public Task<Result<ProposedRepaymentSchedule>> CalculateRepaymentScheduleAsync(ScheduleCalculationRequest request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<Result<ClientAccountSummary>> GetClientAccountsAsync(long clientId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<FineractLoanDetail>> GetLoanDetailAsync(long loanId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<CustomerExposure>> GetCustomerExposureAsync(long clientId, string accountNumber, string customerName, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<FineractClientInfo>> GetClientByIdAsync(long clientId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
