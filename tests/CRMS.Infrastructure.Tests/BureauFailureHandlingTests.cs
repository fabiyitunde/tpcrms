using CRMS.Application.CreditBureau;
using CRMS.Domain.Aggregates.CreditBureau;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Moq;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// How bureau checks fail and are run again: a number no bureau accepts is refused before any billed
/// call, CRC's "could not be completed" with First Central's "no record" reads as no file, the
/// bureaus' own words are kept on the report, a missing score is never made up, and the card's plain
/// heading matches what went wrong.
/// </summary>
public class BureauFailureHandlingTests
{
    private static SmartComplyBusinessCreditReport BusinessReport() => new(
        "b1", "RC123456", "ALLIEDSOFT LTD", null, null, null, null, null, 2, null, null, null,
        new SmartComplyBusinessCreditSummary(1, 1, 0, 1, 0m, 0m, 0m, 0m, 0m, 0, 1, 0, null, null), DateTime.UtcNow);

    private const string CrcCouldNotComplete =
        "API request failed: BadRequest - {\"status\":\"failed\",\"data\":[],\"message\":\"Credit check could not be completed. Please verify your details and try again.\"}";

    // ── Registration number ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("4385473943", "10 digits")]
    [InlineData("RC 123456789", "9 digits")]
    [InlineData("RC12A45", "digits only")]
    public async Task A_number_no_bureau_accepts_is_refused_before_any_call(string rc, string expected)
    {
        var smartComply = new Mock<ISmartComplyProvider>(MockBehavior.Strict);

        var pull = await BureauFallback.GetBusinessReportAsync(smartComply.Object, rc, CancellationToken.None);

        Assert.True(pull.Result.IsFailure);
        Assert.StartsWith("Invalid registration number", pull.Result.Error);
        Assert.Contains(expected, pull.Result.Error);
        Assert.DoesNotContain("not found", pull.Result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("RC123456")]
    [InlineData("rc-1234567")]
    [InlineData("BN 99887766")]
    [InlineData("1234")]
    public void A_proper_number_is_accepted(string rc) => Assert.Null(BureauFallback.InvalidRegistrationNumber(rc));

    // ── CRC "could not be completed" ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Crc_could_not_complete_and_first_central_no_record_reads_as_no_file_with_both_answers()
    {
        var smartComply = new Mock<ISmartComplyProvider>();
        smartComply.Setup(s => s.GetCRCBusinessHistoryAsync("RC123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyBusinessCreditReport>(CrcCouldNotComplete));
        smartComply.Setup(s => s.GetFirstCentralBusinessAsync("RC123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyBusinessCreditReport>("Business not found in credit bureau: No credit history found."));

        var pull = await BureauFallback.GetBusinessReportAsync(smartComply.Object, "RC123456", CancellationToken.None);

        Assert.Contains("not found", pull.Result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Credit check could not be completed", pull.Result.Error);
        Assert.Contains("First Central: no record", pull.Result.Error);
    }

    [Fact]
    public async Task Crc_could_not_complete_with_first_central_failing_too_stays_a_failure()
    {
        var smartComply = new Mock<ISmartComplyProvider>();
        smartComply.Setup(s => s.GetCRCBusinessHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyBusinessCreditReport>(CrcCouldNotComplete));
        smartComply.Setup(s => s.GetFirstCentralBusinessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyBusinessCreditReport>("API request failed: BadRequest - {\"message\":\"Insufficient wallet balance. Please fund your wallet and try again.\"}"));

        var pull = await BureauFallback.GetBusinessReportAsync(smartComply.Object, "RC123456", CancellationToken.None);

        Assert.DoesNotContain("not found", pull.Result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Insufficient wallet balance", pull.Result.Error);
    }

    [Fact]
    public async Task A_first_central_report_still_wins_when_crc_could_not_complete()
    {
        var smartComply = new Mock<ISmartComplyProvider>();
        smartComply.Setup(s => s.GetCRCBusinessHistoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<SmartComplyBusinessCreditReport>(CrcCouldNotComplete));
        smartComply.Setup(s => s.GetFirstCentralBusinessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(BusinessReport()));

        var pull = await BureauFallback.GetBusinessReportAsync(smartComply.Object, "RC123456", CancellationToken.None);

        Assert.True(pull.Result.IsSuccess);
        Assert.Equal(CreditBureauProvider.FirstCentral, pull.Bureau);
    }

    // ── The report keeps the real answer ─────────────────────────────────────────────────────────

    private static BureauReport Individual() =>
        BureauReport.Create(CreditBureauProvider.CRC, SubjectType.Individual, "Ada Obi", "22212345678", Guid.NewGuid()).Value;

    [Fact]
    public void A_not_found_report_keeps_what_the_bureaus_said()
    {
        var report = Individual();

        report.MarkNotFound("Subject not found in credit bureau (CRC, First Central, Credit Registry)");

        Assert.Equal(BureauReportStatus.NotFound, report.Status);
        Assert.Equal("Subject not found in credit bureau (CRC, First Central, Credit Registry)", report.ErrorMessage);
    }

    [Fact]
    public void An_oversized_bureau_answer_is_capped_so_the_report_can_still_be_saved()
    {
        var report = Individual();

        report.MarkFailed(new string('x', 10_000));

        Assert.Equal(BureauReport.MaxErrorMessageLength, report.ErrorMessage!.Length);
    }

    [Fact]
    public void A_report_without_a_bureau_score_is_run_again_and_says_why()
    {
        var report = Individual();
        report.CompleteWithData("r1", null, BureauReport.ScoreUnavailableGrade, DateTime.UtcNow, null, null, 1, 1, 1, 0, 0, 0, 0, 0, 0, false);
        report.NoteScoreUnavailable("First Central score unavailable: SmartComply returned 400: {\"message\":\"Insufficient wallet balance.\"}");

        Assert.True(report.IsScoreMissing);
        Assert.Null(report.CreditScore);
        Assert.Contains("Insufficient wallet balance", report.ErrorMessage);
    }

    [Fact]
    public void A_company_report_has_no_score_and_is_not_run_again_for_it()
    {
        var report = BureauReport.Create(CreditBureauProvider.CRC, SubjectType.Business, "Alliedsoft Ltd", null, Guid.NewGuid(), taxId: "RC123456").Value;
        report.CompleteWithData("r1", null, null, DateTime.UtcNow, null, null, 1, 1, 1, 0, 0, 0, 0, 0, 0, false);

        Assert.False(report.IsScoreMissing);
    }

    [Fact]
    public void An_old_estimated_score_is_run_again_too()
    {
        var report = Individual();
        report.CompleteWithData("r1", 600, BureauReport.LegacyDerivedGrade, DateTime.UtcNow, null, null, 1, 1, 1, 0, 0, 0, 0, 0, 0, false);

        Assert.True(report.IsScoreMissing);
    }

    // ── The card's plain heading ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("SmartComply returned 400: {\"message\":\"Insufficient wallet balance. Please fund your wallet and try again.\"}", "SmartComply wallet balance too low")]
    [InlineData("Invalid registration number \"4385473943\": it has 10 digits", "The RC number cannot be checked")]
    [InlineData("Subject not found in credit bureau. CRC: ...; First Central: no record", "No bureau holds a file")]
    [InlineData("First Central score unavailable: SmartComply returned 500", "The bureau score could not be fetched")]
    [InlineData("Business credit report retrieval failed: CRC: API request failed: BadRequest - Credit check could not be completed", "The bureau could not complete the check")]
    [InlineData("Something nobody expected", "The bureau check did not complete")]
    public void The_heading_says_what_kind_of_problem_it_is(string message, string title)
    {
        Assert.Equal(title, BureauErrors.Explain(message)!.Value.Title);
    }
}
