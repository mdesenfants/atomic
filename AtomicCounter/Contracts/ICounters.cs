using AtomicCounter.Models;
using System.Collections.Generic;

namespace AtomicCounter.Contracts
{
    public interface ICountersCollection : IEnumerable<Counter>
    {
    }
}
