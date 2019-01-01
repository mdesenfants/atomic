using System;
using System.Collections.Generic;

namespace AtomicCounter.Models
{
    public class Tenant
    {
        public string TenantName { get; set; }

        public HashSet<Guid> Profiles { get; set; } = new HashSet<Guid>();

        public HashSet<string> Origins { get; set; } = new HashSet<string>();

        public HashSet<string> WriteKeys { get; set; } = new HashSet<string>();

        public HashSet<string> ReadKeys { get; set; } = new HashSet<string>();
    }
}
