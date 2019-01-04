using System;
using System.Collections.Generic;

namespace AtomicCounter.Models
{
    public class Tenant
    {
        public string TenantName { get; set; }

        public HashSet<Guid> Profiles { get; set; } = new HashSet<Guid>();

        public IList<string> Origins { get; set; } = new List<string>();

        public IList<string> WriteKeys { get; set; } = new List<string>();

        public IList<string> ReadKeys { get; set; } = new List<string>();
    }
}
