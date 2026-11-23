using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// A new financial statements file replaces everything already on the case. Importing year by year
/// left years from an earlier file behind, and skipped a corrected year that was already present,
/// which is how a blank template's zeros outlived the FAC's real figures (RHSHF-2026-714036).
/// </summary>
public class ReplaceRhshfFinancialStatementsHandlerTests
{
    private static RhshfCreditProfile MakeProfile() => RhshfCreditProfile.Create(
        submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
        sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: Guid.NewGuid(),
        companyName: "Gote Agricultural Holdings", rcNumber: "RC123456", tin: "01234567-0001",
        boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
        state: "Kano", lga: "Nassarawa", totalEopValue: 1_000_000m, currency: "NGN", farmerCount: 50,
        callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
        certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
        eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

    /// <summary>Figures with only revenue set; the record has one nullable field per template line.</summary>
    private static RhshfFinancialStatementFigures Figures(decimal revenue)
    {
        var ctor = typeof(RhshfFinancialStatementFigures).GetConstructors().Single();
        var args = new object?[ctor.GetParameters().Length];
        args[0] = (decimal?)revenue;
        return (RhshfFinancialStatementFigures)ctor.Invoke(args);
    }

    private static RhshfFinancialStatement Existing(RhshfCreditProfile profile, int year, bool verified = false)
    {
        var s = RhshfFinancialStatement.Create(profile.Id, year, null, RhshfFinancialYearType.Audited,
            RhshfFinancialInputMethod.ManualEntry, Guid.NewGuid()).Value;
        s.SetFigures(Figures(0m));
        if (verified) s.Verify(Guid.NewGuid(), null);
        return s;
    }

    private static ReplaceRhshfFinancialStatementsCommand Import(RhshfCreditProfile profile, params int[] years) => new(
        profile.Reference, Guid.NewGuid(), "NGN",
        years.Select(y => new RhshfFinancialStatementImportYear(y, RhshfFinancialYearType.Audited, Figures(11_735_000m))).ToList(),
        "FinancialStatementTemplate.xlsx");

    [Fact]
    public async Task A_new_file_replaces_every_statement_on_the_case()
    {
        var profile = MakeProfile();
        var repo = new FakeStatementRepository([Existing(profile, 2022), Existing(profile, 2023), Existing(profile, 2024)]);
        var handler = new ReplaceRhshfFinancialStatementsHandler(new FakeProfileRepository(profile), repo, new FakeUnitOfWork());

        var result = await handler.Handle(Import(profile, 2023, 2024, 2025));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.Imported);
        Assert.Equal(3, result.Data.Replaced);
        Assert.Equal([2023, 2024, 2025], repo.Statements.Select(s => s.FinancialYear).OrderBy(y => y));
        Assert.All(repo.Statements, s => Assert.Equal(11_735_000m, s.Revenue));
    }

    [Fact]
    public async Task Verified_years_that_are_replaced_are_reported()
    {
        var profile = MakeProfile();
        var repo = new FakeStatementRepository([Existing(profile, 2024, verified: true), Existing(profile, 2023)]);
        var handler = new ReplaceRhshfFinancialStatementsHandler(new FakeProfileRepository(profile), repo, new FakeUnitOfWork());

        var result = await handler.Handle(Import(profile, 2025));

        Assert.Equal([2024], result.Data!.VerifiedYearsReplaced);
        Assert.Equal([2025], repo.Statements.Select(s => s.FinancialYear));
    }

    [Fact]
    public async Task A_file_with_a_repeated_year_changes_nothing()
    {
        var profile = MakeProfile();
        var repo = new FakeStatementRepository([Existing(profile, 2022)]);
        var handler = new ReplaceRhshfFinancialStatementsHandler(new FakeProfileRepository(profile), repo, new FakeUnitOfWork());

        var result = await handler.Handle(Import(profile, 2025, 2025));

        Assert.False(result.IsSuccess);
        Assert.Equal([2022], repo.Statements.Select(s => s.FinancialYear));
    }

    [Fact]
    public async Task An_empty_file_changes_nothing()
    {
        var profile = MakeProfile();
        var repo = new FakeStatementRepository([Existing(profile, 2022)]);
        var handler = new ReplaceRhshfFinancialStatementsHandler(new FakeProfileRepository(profile), repo, new FakeUnitOfWork());

        var result = await handler.Handle(Import(profile));

        Assert.False(result.IsSuccess);
        Assert.Single(repo.Statements);
    }

    private class FakeStatementRepository(List<RhshfFinancialStatement> statements) : IRhshfFinancialStatementRepository
    {
        public List<RhshfFinancialStatement> Statements { get; } = statements;

        public Task<IReadOnlyList<RhshfFinancialStatement>> GetByProfileIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfFinancialStatement>>(Statements.Where(s => s.RhshfCreditProfileId == id).ToList());
        public Task<RhshfFinancialStatement?> GetByProfileAndYearAsync(Guid id, int year, CancellationToken ct = default)
            => Task.FromResult(Statements.FirstOrDefault(s => s.RhshfCreditProfileId == id && s.FinancialYear == year));
        public Task<RhshfFinancialStatement?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Statements.FirstOrDefault(s => s.Id == id));
        public Task AddAsync(RhshfFinancialStatement statement, CancellationToken ct = default)
        {
            Statements.Add(statement);
            return Task.CompletedTask;
        }
        public void Remove(RhshfFinancialStatement statement) => Statements.Remove(statement);
    }

    private class FakeProfileRepository(RhshfCreditProfile profile) : IRhshfCreditProfileRepository
    {
        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RhshfCreditProfile?>(profile);
        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult(profile.Reference == reference ? profile : null);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(RhshfCreditProfile p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RhshfReusableDocument>> GetReusableDocumentsForFacAsync(Guid facId, Guid excludeProfileId, RhshfDocumentCategory category, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
