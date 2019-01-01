using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtomicCounter.Models.ViewModels
{
    public class TenantViewModel
    {
        public string TenantName { get; set; }

        public IEnumerable<string> Origins { get; set; } = new HashSet<string>();

        public IEnumerable<string> WriteKeys { get; set; } = new HashSet<string>();

        public IEnumerable<string> ReadKeys { get; set; } = new HashSet<string>();
    }
}
