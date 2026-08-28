using CRMS.Domain.Common;

namespace CRMS.Domain.Aggregates.Rhshf;

/// <summary>
/// One commodity line from the FAC's consolidated EOP (Estimate of Production). Context-only —
/// not independently workflow-driven. Own entity, independent of any NAMP equipment line item.
/// </summary>
public class RhshfEopLine : Entity
{
    public Guid RhshfCreditProfileId { get; private set; }
    public string Commodity { get; private set; } = string.Empty;
    public decimal QuantityKg { get; private set; }
    public decimal UnitPricePerKg { get; private set; }
    public decimal LineValue { get; private set; }

    protected RhshfEopLine() { }

    public RhshfEopLine(Guid rhshfCreditProfileId, string commodity, decimal quantityKg, decimal unitPricePerKg, decimal lineValue)
    {
        RhshfCreditProfileId = rhshfCreditProfileId;
        Commodity = commodity;
        QuantityKg = quantityKg;
        UnitPricePerKg = unitPricePerKg;
        LineValue = lineValue;
    }
}
