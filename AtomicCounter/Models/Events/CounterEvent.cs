using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;

namespace AtomicCounter.Models.Events
{
    public class CounterEvent
    {
        [Required(ErrorMessage = "Counter name is required.")]
        public string Counter { get; set; }

        public Guid EventId { get; set; } = Guid.NewGuid();

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}