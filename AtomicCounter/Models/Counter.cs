using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace AtomicCounter.Models
{
    public enum InvoiceFrequency
    {
        Never,
        Weekly,
        EveryOtherWeek,
        TwiceMonthly,
        Monthly,
        Quarterly,
        TwiceAnnually,
        Annually
    }

    public class Counter
    {
        public string CounterName { get; set; }
        public HashSet<Guid> Profiles { get; } = new HashSet<Guid>();
        public IList<string> WriteKeys { get; } = new List<string>();
        public IList<string> ReadKeys { get; } = new List<string>();
        public DateTimeOffset LastInvoiceRun { get; set; } = DateTimeOffset.MinValue;
        public DateTimeOffset NextInvoiceRun { get; set; } = DateTimeOffset.MaxValue;
        public bool AutoSubmit { get; set; } = false;

        [JsonConverter(typeof(StringEnumConverter))]
        public InvoiceFrequency InvoiceFrequency { get; set; } = InvoiceFrequency.Never;
    }
}
