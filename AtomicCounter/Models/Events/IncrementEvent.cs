namespace AtomicCounter.Models.Events
{
    public class IncrementEvent : CounterEvent
    {
        public long Count { get; set; } = 1;
        public string Client { get; set; }
    }
}
