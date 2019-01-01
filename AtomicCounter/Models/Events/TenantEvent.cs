using Newtonsoft.Json;

namespace AtomicCounter.Models.Events
{
    public class TenantEvent
    {
        public string Tenant { get; set; }
        public string App { get; set; }
        public string Counter { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}