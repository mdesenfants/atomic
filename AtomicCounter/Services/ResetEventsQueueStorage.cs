using System.Threading.Tasks;

namespace AtomicCounter.Services
{
    public class ResetEventsQueueStorage : AppStorage
    {
        public override async Task CreateStorage()
        {
            var resetQueue = Queues.GetQueueReference(ResetEventsQueueName);
            await resetQueue.CreateIfNotExistsAsync();
        }
    }
}
