using CRMS.Application.CustomerCheck;
using CRMS.Domain.Aggregates.Access;
using CRMS.Domain.Constants;
using CRMS.Domain.Interfaces;
using Xunit;

namespace CRMS.Application.Tests.CustomerCheck;

/// <summary>The company credit check's pure rules, and who a feature-access rule lets in.</summary>
public class CompanyCheckRulesTests
{
    private static readonly CustomerCheckSettings Settings = new();

    [Theory]
    [InlineData("RC123456", "RC123456", "RC")]
    [InlineData("rc 123456", "RC123456", "RC")]
    [InlineData("RC-123456", "RC123456", "RC")]
    [InlineData("123456", "RC123456", "RC")]
    [InlineData("BN 998877", "BN998877", "BN")]
    [InlineData("IT/4455", "IT4455", "IT")]
    public void Registration_numbers_keep_their_prefix_and_lose_separators(string input, string number, string type)
    {
        var normalised = CompanyCheckRules.NormaliseRegistrationNumber(input);

        Assert.NotNull(normalised);
        Assert.Equal(number, normalised!.Value.Number);
        Assert.Equal(type, normalised.Value.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC123")]
    [InlineData("RC12A45")]
    public void Anything_else_is_not_a_registration_number(string input)
        => Assert.Null(CompanyCheckRules.NormaliseRegistrationNumber(input));

    [Fact]
    public void A_case_record_is_searched_under_every_common_spelling()
    {
        var spellings = CompanyCheckRules.StoredSpellings("RC123456", "RC");

        Assert.Contains("RC123456", spellings);
        Assert.Contains("123456", spellings);
        Assert.Contains("RC 123456", spellings);
        Assert.Contains("RC-123456", spellings);
    }

    [Fact]
    public void A_company_request_needs_the_number_and_the_name()
    {
        var (query, errors) = CompanyCheckRules.Validate(new CompanyCheckRequest("r1", null, null, null, null, null), Settings);

        Assert.Null(query);
        Assert.Contains("rcNumber is required.", errors);
        Assert.Contains("companyName is required.", errors);
    }

    [Fact]
    public void A_valid_company_request_runs_every_section_by_default()
    {
        var (query, errors) = CompanyCheckRules.Validate(
            new CompanyCheckRequest("r1", "rc 123456", " Gote Agro Ltd ", ["0123456789"], null,
                new CompanyCheckOptions(null, null, 5_000_000m, 12, 40_000_000m, "Inputs")), Settings);

        Assert.Empty(errors);
        Assert.Equal("RC123456", query!.RcNumber);
        Assert.Equal("123456", query.RcDigits);
        Assert.Equal("Gote Agro Ltd", query.CompanyName);
        Assert.Equal(CompanyCheckSections.All, query.Sections);
        Assert.Equal(40_000_000m, query.AnnualRevenue);
    }

    [Theory]
    [InlineData("Gote Agricultural Holdings Limited", "GOTE AGRICULTURAL HOLDINGS LTD", true)]
    [InlineData("Alliedsoft Ltd.", "alliedsoft limited", true)]
    [InlineData("Alliedsoft Ltd", "Allied Farms Ltd", false)]
    public void Registered_names_are_compared_without_punctuation_or_suffix_spelling(string a, string b, bool same)
        => Assert.Equal(same, CompanyCheckRules.SameName(a, b));

    [Fact]
    public void A_registered_name_that_differs_is_reported_and_warned_about()
    {
        var registration = CompanyCheckRules.MapRegistration(Cac("ALLIED FARMS LTD", "INACTIVE"), "Alliedsoft Ltd");

        Assert.False(registration.NameMatches);
        Assert.False(registration.IsActive);
        var warnings = CompanyCheckRules.Warnings(Response(registration));
        Assert.Contains(warnings, w => w.Contains("not active"));
        Assert.Contains(warnings, w => w.Contains("differs from the name given"));
    }

    [Fact]
    public void Directors_are_listed_with_the_chairman_first()
    {
        var cac = Cac("ALLIEDSOFT LTD", "ACTIVE") with
        {
            Directors =
            [
                Director("Bola Ade", "DIRECTOR", chairman: false),
                Director("Ade Musa", "DIRECTOR", chairman: true),
            ],
        };

        var registration = CompanyCheckRules.MapRegistration(cac, "Alliedsoft Ltd");

        Assert.True(registration.IsActive);
        Assert.Equal("Ade Musa", registration.Affiliates[0].Name);
        Assert.True(registration.Affiliates[0].IsChairman);
    }

    [Fact]
    public void A_company_advisory_is_scored_as_a_company()
    {
        var (query, _) = CompanyCheckRules.Validate(new CompanyCheckRequest("r1", "RC1", "A Ltd", null, null, null), Settings);
        var input = CompanyCheckRules.AdvisoryInput(Guid.NewGuid(), query!, Settings,
            CompanyCheckRules.MapRegistration(Cac("A LTD", "ACTIVE"), "A Ltd"),
            CreditBureauSection.Empty(SectionStatuses.NotRequested), FraudCheckSection.Empty(SectionStatuses.NotRequested),
            BankAccountsSection.Empty(SectionStatuses.NotPossible), BankLoansSection.Empty(SectionStatuses.NotPossible),
            CrmsFacilitiesSection.Empty(SectionStatuses.NotRequested), null, DateTime.UtcNow);

        Assert.True(input.IsCompany);
        Assert.True(input.Identity.BvnVerified);
        Assert.Null(input.Identity.WatchListed);
    }

    // ── Feature access ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_rule_keeps_only_roles_the_bank_manages()
    {
        var rule = FeatureAccessRule.Create(FeatureKeys.CreditCheckRun,
            [Roles.CreditOfficer, Roles.SystemAdmin, Roles.BillingManager, Roles.Customer, "Nonsense", Roles.CreditOfficer]);

        Assert.Equal([Roles.CreditOfficer], rule.RoleList);
    }

    [Fact]
    public void A_system_administrator_is_always_let_in()
    {
        var rule = FeatureAccessRule.Create(FeatureKeys.CreditCheckRun);

        Assert.True(rule.Allows([Roles.SystemAdmin]));
        Assert.False(rule.Allows([Roles.CreditOfficer]));
    }

    [Fact]
    public void A_ticked_role_is_let_in_and_an_unticked_one_is_not()
    {
        var rule = FeatureAccessRule.Create(FeatureKeys.CreditCheckRun, [Roles.CreditOfficer, Roles.RiskManager]);

        Assert.True(rule.Allows([Roles.LoanOfficer, Roles.RiskManager]));
        Assert.False(rule.Allows([Roles.LoanOfficer]));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private static SmartComplyCacResult Cac(string name, string status) => new(
        name, "123456", "RC", "2015-03-01", "1 Farm Road", "Kano", "Kano", "info@a.ng", status, "Agriculture", 1_000_000m, 1, []);

    private static SmartComplyCacDirector Director(string name, string role, bool chairman) => new(
        null, null, null, null, name, null, null, "Nigerian", null, null, null, null, null, null, null,
        "ACTIVE", chairman, false, "2015-03-01", role, null, null, null, null);

    private static CompanyCheckResponse Response(CompanyRegistrationSection registration) => new(
        "r1", DateTime.UtcNow, "test", new CompanySubject("RC123456", "Alliedsoft Ltd", []), [],
        registration, CreditBureauSection.Empty(SectionStatuses.NotRequested), FraudCheckSection.Empty(SectionStatuses.NotRequested),
        BankAccountsSection.Empty(SectionStatuses.NotRequested), BankLoansSection.Empty(SectionStatuses.NotRequested),
        CrmsFacilitiesSection.Empty(SectionStatuses.NotRequested), AiAdvisorySection.Empty(SectionStatuses.NotRequested),
        new Dictionary<string, string>(), []);
}
