using CRMS.Application.CreditBureau;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using CRMS.Infrastructure.ExternalServices.SmartComply;
using Moq;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Individual pulls go CRC, then First Central, then Credit Registry; business pulls go CRC, then
/// First Central. Each step runs when the one before had no record or failed. "Not found" must
/// only survive when every bureau tried said so, because the handlers file a "not found" pull as a
/// clean no-record outcome rather than a failure.
/// </summary>
public class BureauFallbackTests
{
    private const string Bvn = "22222222222";
    private const string Rc = "RC123456";
    private const string NotFound = "Subject not found in credit bureau";

    private static SmartComplyIndividualCreditReport Report(string name) => new(
        name, Bvn, name, null, null, null, null, null,
        new SmartComplyCreditSummary(1, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, null),
        [], [], [], [], null);

    private static SmartComplyBusinessCreditReport Business(string name) => new(
        name, Rc, name, null, null, null, null, null, 0, null, null, null,
        new SmartComplyBusinessCreditSummary(1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, null, null),
        null);

    private static Result<SmartComplyIndividualCreditReport> Fail(string error) =>
        Result.Failure<SmartComplyIndividualCreditReport>(error);

    /// <summary>A null error means that bureau answers with a report named after it.</summary>
    private static Mock<ISmartComplyProvider> Individual(string? crc, string? fc, string? cr)
    {
        var sc = new Mock<ISmartComplyProvider>(MockBehavior.Strict);
        sc.Setup(s => s.GetCRCFullAsync(Bvn, It.IsAny<CancellationToken>())).ReturnsAsync(crc is null ? Result.Success(Report("crc")) : Fail(crc));
        sc.Setup(s => s.GetFirstCentralFullAsync(Bvn, It.IsAny<CancellationToken>())).ReturnsAsync(fc is null ? Result.Success(Report("fc")) : Fail(fc));
        sc.Setup(s => s.GetCreditRegistryFullAsync(Bvn, It.IsAny<CancellationToken>())).ReturnsAsync(cr is null ? Result.Success(Report("cr")) : Fail(cr));
        return sc;
    }

    [Fact]
    public async Task Crc_answering_means_no_other_bureau_is_called()
    {
        var sc = Individual(null, null, null);

        var pull = await BureauFallback.GetIndividualReportAsync(sc.Object, Bvn, default);

        Assert.True(pull.Result.IsSuccess);
        Assert.Equal(CreditBureauProvider.CRC, pull.Bureau);
        Assert.Equal("crc", pull.Result.Value.Name);
        sc.Verify(s => s.GetFirstCentralFullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        sc.Verify(s => s.GetCreditRegistryFullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(NotFound)]
    [InlineData("API request failed (500): upstream timeout")]
    public async Task Crc_with_no_record_or_an_error_falls_back_to_First_Central(string crcError)
    {
        var sc = Individual(crcError, null, null);

        var pull = await BureauFallback.GetIndividualReportAsync(sc.Object, Bvn, default);

        Assert.True(pull.Result.IsSuccess);
        Assert.Equal(CreditBureauProvider.FirstCentral, pull.Bureau);
        Assert.Equal("fc", pull.Result.Value.Name);
        sc.Verify(s => s.GetCreditRegistryFullAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(NotFound, NotFound)]
    [InlineData("API request failed (500): upstream timeout", "Credit check service is temporarily unavailable")]
    public async Task Crc_and_First_Central_both_missing_falls_back_to_Credit_Registry(string crcError, string fcError)
    {
        var sc = Individual(crcError, fcError, null);

        var pull = await BureauFallback.GetIndividualReportAsync(sc.Object, Bvn, default);

        Assert.True(pull.Result.IsSuccess);
        Assert.Equal(CreditBureauProvider.CreditRegistry, pull.Bureau);
        Assert.Equal("cr", pull.Result.Value.Name);
    }

    [Fact]
    public async Task No_record_at_all_three_bureaus_reads_as_not_found()
    {
        var sc = Individual(NotFound, NotFound, NotFound);

        var pull = await BureauFallback.GetIndividualReportAsync(sc.Object, Bvn, default);

        Assert.True(pull.Result.IsFailure);
        Assert.Contains("not found", pull.Result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Credit Registry", pull.Result.Error);
    }

    [Theory]
    [InlineData(NotFound, "Connection error: refused", NotFound)]
    [InlineData("Request timed out", NotFound, NotFound)]
    [InlineData(NotFound, NotFound, "the connection is lost")]
    public async Task One_bureau_failing_outright_is_a_failure_not_a_clean_no_record(string crcError, string fcError, string crError)
    {
        var sc = Individual(crcError, fcError, crError);

        var pull = await BureauFallback.GetIndividualReportAsync(sc.Object, Bvn, default);

        Assert.True(pull.Result.IsFailure);
        Assert.DoesNotContain("not found", pull.Result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CRC:", pull.Result.Error);
        Assert.Contains("First Central:", pull.Result.Error);
        Assert.Contains("Credit Registry:", pull.Result.Error);
    }

    [Fact]
    public async Task The_score_comes_from_the_bureau_that_answered()
    {
        var sc = new Mock<ISmartComplyProvider>(MockBehavior.Strict);
        sc.Setup(s => s.GetFirstCentralScoreAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new SmartComplyCreditScore(712, "B", "FirstCentral", null)));
        sc.Setup(s => s.GetCRCScoreAsync(Bvn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new SmartComplyCreditScore(650, "C", "CRC", null)));

        Assert.Equal(712, (await BureauFallback.GetScoreAsync(sc.Object, CreditBureauProvider.FirstCentral, Bvn, default)).Value.Score);
        Assert.Equal(650, (await BureauFallback.GetScoreAsync(sc.Object, CreditBureauProvider.CRC, Bvn, default)).Value.Score);
    }

    [Fact]
    public async Task A_Credit_Registry_report_gets_no_score_and_no_other_bureau_is_asked_for_one()
    {
        // Strict with no setups: asking CRC or First Central for a score would throw.
        var sc = new Mock<ISmartComplyProvider>(MockBehavior.Strict);

        var score = await BureauFallback.GetScoreAsync(sc.Object, CreditBureauProvider.CreditRegistry, Bvn, default);

        Assert.True(score.IsFailure);
    }

    [Fact]
    public async Task Business_pulls_fall_back_to_First_Central_and_stop_there()
    {
        var sc = new Mock<ISmartComplyProvider>(MockBehavior.Strict);
        sc.Setup(s => s.GetCRCBusinessHistoryAsync(Rc, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<SmartComplyBusinessCreditReport>(NotFound));
        sc.Setup(s => s.GetFirstCentralBusinessAsync(Rc, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(Business("fc")));

        var pull = await BureauFallback.GetBusinessReportAsync(sc.Object, Rc, default);

        Assert.True(pull.Result.IsSuccess);
        Assert.Equal(CreditBureauProvider.FirstCentral, pull.Bureau);
    }

    [Fact]
    public async Task No_record_at_both_business_bureaus_reads_as_not_found()
    {
        var sc = new Mock<ISmartComplyProvider>(MockBehavior.Strict);
        sc.Setup(s => s.GetCRCBusinessHistoryAsync(Rc, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<SmartComplyBusinessCreditReport>(NotFound));
        sc.Setup(s => s.GetFirstCentralBusinessAsync(Rc, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<SmartComplyBusinessCreditReport>(NotFound));

        var pull = await BureauFallback.GetBusinessReportAsync(sc.Object, Rc, default);

        Assert.True(pull.Result.IsFailure);
        Assert.Contains("not found", pull.Result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("RC1275857", "RC1275857")]
    [InlineData("1275857", "RC1275857")]
    [InlineData(" rc-1275857 ", "RC1275857")]
    [InlineData("BN123456", "BN123456")]
    [InlineData("bn 123456", "BN123456")]
    [InlineData("IT98765", "IT98765")]
    public void Business_registration_numbers_keep_their_own_prefix(string stored, string sent) =>
        Assert.Equal(sent, SmartComplyProvider.BureauRegistrationNumber(stored));
}
