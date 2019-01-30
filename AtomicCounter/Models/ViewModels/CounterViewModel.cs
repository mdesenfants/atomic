using System.Collections.Generic;

namespace AtomicCounter.Models.ViewModels
{
    public class CounterViewModel
    {
        public string CounterName { get; set; }

        public IEnumerable<string> Origins { get; set; } = new List<string>();

        public IEnumerable<string> WriteKeys { get; set; } = new List<string>();

        public IEnumerable<string> ReadKeys { get; set; } = new List<string>();

        public IEnumerable<IPriceChange> PriceChanges { get; set; } = new List<IPriceChange>();
    }
}
