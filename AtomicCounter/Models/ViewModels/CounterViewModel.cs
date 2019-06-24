using System.Collections.Generic;
using System.Linq;

namespace AtomicCounter.Models.ViewModels
{
    public class CounterViewModel
    {
        public string CounterName { get; set; }

        public string CounterCanonicalName => CounterName.ToCanonicalName();

        public IEnumerable<string> Origins { get; set; } = new List<string>();

        public IEnumerable<string> WriteKeys { get; set; } = new List<string>();

        public IEnumerable<string> ReadKeys { get; set; } = new List<string>();
    }
}
