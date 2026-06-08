using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

public class NampStatusHistory : Entity
{
    public Guid NampApplicationId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTime ChangedAt { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public string? Note { get; private set; }

    private NampStatusHistory() { }

    internal static NampStatusHistory Create(
        Guid nampApplicationId,
        NampApplicationStatus status,
        Guid changedByUserId,
        string? note = null)
    {
        return new NampStatusHistory
        {
            NampApplicationId = nampApplicationId,
            Status = status.ToString(),
            ChangedByUserId = changedByUserId,
            Note = note,
            ChangedAt = DateTime.UtcNow,
        };
    }
}
