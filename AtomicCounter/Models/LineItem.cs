using System;

namespace AtomicCounter.Models
{
    public class ChargeGroup
    {
        public DateTimeOffset Effective { get; set; }
        public decimal Price { get; set; }
        public long Quantity { get; set; }
    }
}
