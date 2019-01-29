using System;

namespace AtomicCounter.Models
{
    public interface IPriceChange
    {
        string Counter { get; set; }
        decimal Amount { get; set; }
        string Currency { get; set; }
        DateTimeOffset? Effective { get; set; }
        DateTimeOffset Timestamp { get; set; }
    }
}
