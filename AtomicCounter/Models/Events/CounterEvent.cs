using Newtonsoft.Json;

namespace AtomicCounter.Models.Events
{
    public class CounterEvent
    {
        public string Counter { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}