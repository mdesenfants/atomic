using AtomicCounter.Models.Events;
using System;
using System.Collections.Generic;

namespace AtomicCounter.Models
{
    public enum InvoiceFrequency
    {
        Never,
        Weekly,
        TwiceMonthly,
        Monthly,
        Quarterly,
        TwiceAnnually,
        Annually
    }

    public class Counter
    {
        public string CounterName { get; set; }
        public HashSet<Guid> Profiles { get; set; } = new HashSet<Guid>();
        public IList<string> WriteKeys { get; set; } = new List<string>();
        public IList<string> ReadKeys { get; set; } = new List<string>();
        public IList<PriceChangeEvent> PriceChanges { get; set; } = new List<PriceChangeEvent>();
        public DateTimeOffset LastInvoiceRun { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset NextInvoiceRun { get; set; } = DateTimeOffset.MaxValue;
        public InvoiceFrequency InvoiceFrequency { get; set; } = InvoiceFrequency.Never;
        public bool AutoSubmit { get; set; } = false;
    }
}
