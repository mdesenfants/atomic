using System;
using System.Collections.Generic;

namespace AtomicCounter.Models
{
    public class UserProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Email { get; set; }

        public IEnumerable<string> Counters { get; set; } = new List<string>();
    }
}
