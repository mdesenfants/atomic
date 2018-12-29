namespace AtomicCounter.Models.Events
{
    public class IncrementEvent : TenantEvent
    {
        public long Count { get; set; } = 1;
    }
}
