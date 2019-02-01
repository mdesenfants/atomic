using System;

namespace AtomicCounter.Models.Events
{
    public class InvoiceRequestEvent : CounterEvent
    {
        public DateTimeOffset Min { get; set; }
        public DateTimeOffset Max { get; set; }
    }
}
