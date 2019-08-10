using System.Threading.Tasks;

namespace AtomicCounter.Services
{
    public class CountQueueStorage : AppStorage
    {
        public override async Task CreateStorage()
        {
            var queue = Queues.GetQueueReference(CountQueueName);
            await queue.CreateIfNotExistsAsync().ConfigureAwait(false);
        }
    }
}
