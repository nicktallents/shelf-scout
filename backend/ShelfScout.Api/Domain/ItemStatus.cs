namespace ShelfScout.Api.Domain;

/// <summary>
/// Computed lifecycle state (never persisted). Precedence: Removed (Consumed | Discarded) →
/// Expired → Expiring Soon → Fresh (ADR 0003).
/// </summary>
public enum ItemStatus
{
    Fresh,
    ExpiringSoon,
    Expired,
    Consumed,
    Discarded,
}
