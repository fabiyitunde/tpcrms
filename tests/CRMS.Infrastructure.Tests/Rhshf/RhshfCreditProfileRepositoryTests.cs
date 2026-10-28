using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Infrastructure.Persistence;
using CRMS.Infrastructure.Persistence.Repositories.Rhshf;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>
/// Loads a case through the real repository on a fresh context. Every other RH-SHF test builds the
/// aggregate in memory and never goes near the repository, which is how a missing Include shipped:
/// the portal's itemised breakdown was saved but never loaded, so the offer letter printed the
/// crop-level fallback for a case that had the full breakdown on file (CRMSQA#2).
/// </summary>
public class RhshfCreditProfileRepositoryTests
{
    private const decimal Inputs = 1_000_000.00m;
    private const decimal Insurance = 40_000.00m;
    private const decimal Total = Inputs + Insurance;

    [Fact]
    public async Task GetByReference_LoadsThePortalsItemisedBreakdown()
    {
        var dbName = "RhshfRepo_" + Guid.NewGuid();
        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: null,
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: Total, currency: "NGN", farmerCount: 30,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: null, certifiedAt: null, rawSubmissionPayload: "{}",
            eopLines: [new RhshfEopLineInput("Maize", 0m, 0m, Total)],
            resolvedBranchId: null, resolvedOfficeId: null,
            financing: new RhshfFinancingInput(
                [new RhshfFinancingLineInput(Guid.NewGuid(), "Maize", "NPK Fertilizer", "Fertilizer", null,
                    "AgroEquip Solutions Ltd", 2m, "Kg", 1_120m, "perFarmer", 30, Inputs, 0m, true)],
                [new RhshfFinancingChargeInput("insurance", "Insurance", 4m, "4% of inputs", Insurance, true)],
                InputsTotal: Inputs, PackagingExcessTotal: 0m, ChargesTotal: Insurance,
                ApprovedEopTotal: Total, LoanAmount: Total, ExcludedFromLoan: 0m)).Value;

        await using (var write = NewContext(dbName))
        {
            write.RhshfCreditProfiles.Add(profile);
            await write.SaveChangesAsync();
        }

        await using var read = NewContext(dbName);
        var loaded = await new RhshfCreditProfileRepository(read).GetByReferenceAsync(profile.Reference);

        Assert.NotNull(loaded);
        Assert.True(loaded!.HasItemisedFinancing);
        Assert.Equal("AgroEquip Solutions Ltd", Assert.Single(loaded.FinancingLines).Supplier);
        Assert.Equal("Insurance", Assert.Single(loaded.FinancingCharges).Label);
    }

    private static CRMSDbContext NewContext(string dbName) =>
        new(new DbContextOptionsBuilder<CRMSDbContext>().UseInMemoryDatabase(dbName).Options);
}
