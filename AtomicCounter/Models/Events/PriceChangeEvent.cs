using System;
using System.ComponentModel.DataAnnotations;

namespace AtomicCounter.Models.Events
{
    public class PriceChangeEvent : CounterEvent, IPriceChange
    {
        [Required(ErrorMessage = "Cost of service is required.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "ISO currency code is required.")]
        public string Currency { get; set; }

        public DateTimeOffset? Effective { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }
}
