using System;
using System.Collections.Generic;

namespace AtomicCounter.Models
{
    public class UserProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Email { get; set; } = String.Empty;

        public IList<string> Counters { get; } = new List<string>();
    }
}
