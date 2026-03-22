namespace CRMS.Web.Intranet.Services;

public class CollateralHaircutSettings
{
    public const string SectionName = "CollateralHaircuts";

    public decimal CashDeposit { get; set; } = 0;
    public decimal FixedDeposit { get; set; } = 5;
    public decimal TreasuryBills { get; set; } = 5;
    public decimal Bonds { get; set; } = 10;
    public decimal RealEstate { get; set; } = 20;
    public decimal Vehicle { get; set; } = 30;
    public decimal Stocks { get; set; } = 30;
    public decimal Equipment { get; set; } = 40;
    public decimal Inventory { get; set; } = 50;
    public decimal Default { get; set; } = 20;
}
