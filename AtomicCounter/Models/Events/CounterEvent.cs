using Newtonsoft.Json;
using System;

namespace AtomicCounter.Models.Events
{
    public class CounterEvent
    {
        public string Counter { get; set; }

        public Guid EventId { get; set; } = Guid.NewGuid();

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}