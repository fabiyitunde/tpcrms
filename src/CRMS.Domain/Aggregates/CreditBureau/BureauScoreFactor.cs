using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.CreditBureau;

public class BureauScoreFactor : Entity
{
    public Guid BureauReportId { get; private set; }
    public string FactorCode { get; private set; } = string.Empty;
    public string FactorDescription { get; private set; } = string.Empty;
    public string Impact { get; private set; } = string.Empty;
    public int? Rank { get; private set; }

    private BureauScoreFactor() { }

    public static BureauScoreFactor Create(
        Guid bureauReportId,
        string factorCode,
        string factorDescription,
        string impact,
        int? rank = null)
    {
        return new BureauScoreFactor
        {
            BureauReportId = bureauReportId,
            FactorCode = factorCode,
            FactorDescription = factorDescription,
            Impact = impact,
            Rank = rank
        };
    }
}
