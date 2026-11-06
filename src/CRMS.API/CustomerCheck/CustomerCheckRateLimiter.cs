using CRMS.API.PartyExposure;

namespace CRMS.API.CustomerCheck;

/// <summary>
/// Per-consumer and overall limits for the customer credit check, in one-minute windows. Its own instance
/// so partner traffic and portal traffic never count against each other. Same mechanics as the
/// party-exposure limiter: in memory, single API instance.
/// </summary>
public class CustomerCheckRateLimiter : PartyExposureRateLimiter
{
    public CustomerCheckRateLimiter(TimeProvider? clock = null) : base(clock) { }
}
