using CRMS.Application.CreditBureau;
using CRMS.Domain.Common;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;
using Moq;
using Xunit;

namespace CRMS.Infrastructure.Tests;

/// <summary>
/// Every loan track pulls CRC first and falls back to First Central when CRC has no record or the
/// CRC call fails. "Not found" must only survive when both bureaus said so, because the handlers
/// file a "not found" pull as a clean no-record outcome rather than a failure.
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

    [Fact]
    public async Task Crc_answering_means_First_Central_is_never_called()
    {
        var sc = new Mock<ISmartComplyProvider>(MockBehavior.Strict);
        sc.Setup(s => s.GetCRCFullAsync(Bvn, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(Report("crc")));

        var pull = await BureauFallback.GetIndividualReportAsync(sc.Object, Bvn, default);

        Assert.True(pull.Result.IsSuccess);
        Assert.Equal(CreditBureauProvider.CRC, pull.Bureau);
        Assert.Equal("crc", pull.Result.Value.Name);
    }

    [Theory]
    [InlineData(NotFound)]
    [InlineData("API request failed (500): upstream timeout")]
    public async Task Crc_with_no_record_or_an_error_falls_back_to_First_Central(string crcError)
    {
        var sc = new Mock<ISmartComplyProvider>(MockBehavior.Strict);
        sc.Setup(s => s.GetCRCFullAsync(Bvn, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<SmartComplyIndividualCreditReport>(crcError));
        sc.Setup(s => s.GetFirstCentralFullAsync(Bvn, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(Report("fc")));

        var pull = await BureauFallback.GetIndividualReportAsync(sc.Object, Bvn, default);

        Assert.True(pull.Result.IsSuccess);
        Assert.Equal(CreditBureauProvider.FirstCentral, pull.Bureau);
        Assert.Equal("fc", pull.Result.Value.Name);
    }

    [Fact]
    public async Task No_record_at_both_bureaus_reads_as_not_found()
    {
        var sc = new Mock<ISmartComplyProvider>();
        sc.Setup(s => s.GetCRCFullAsync(Bvn, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<SmartComplyIndividualCreditReport>(NotFound));
        sc.Setup(s => s.GetFirstCentralFullAsync(Bvn, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<SmartComplyIndividualCreditReport>(NotFound));

        var pull = await BureauFallback.GetIndividualReportAsync(sc.Object, Bvn, default);

        Assert.True(pull.Result.IsFailure);
        Assert.Contains("not found", pull.Result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(NotFound, "Connection error: refused")]
    [InlineData("Request timed out", NotFound)]
    public async Task One_bureau_failing_outright_is_a_failure_not_a_clean_no_record(string crcError, string fcError)
    {
        var sc = new Mock<ISmartComplyProvider>();
        sc.Setup(s => s.GetCRCFullAsync(Bvn, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<SmartComplyIndividualCreditReport>(crcError));
        sc.Setup(s => s.GetFirstCentralFullAsync(Bvn, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<SmartComplyIndividualCreditReport>(fcError));

        var pull = await BureauFallback.GetIndividualReportAsync(sc.Object, Bvn, default);

        Assert.True(pull.Result.IsFailure);
        Assert.DoesNotContain("not found", pull.Result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CRC:", pull.Result.Error);
        Assert.Contains("First Central:", pull.Result.Error);
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
    public async Task Business_pulls_fall_back_the_same_way()
    {
        var sc = new Mock<ISmartComplyProvider>(MockBehavior.Strict);
        sc.Setup(s => s.GetCRCBusinessHistoryAsync(Rc, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure<SmartComplyBusinessCreditReport>(NotFound));
        sc.Setup(s => s.GetFirstCentralBusinessAsync(Rc, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(Business("fc")));

        var pull = await BureauFallback.GetBusinessReportAsync(sc.Object, Rc, default);

        Assert.True(pull.Result.IsSuccess);
        Assert.Equal(CreditBureauProvider.FirstCentral, pull.Bureau);
    }
}
