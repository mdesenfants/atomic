using System.Threading.Tasks;

namespace AtomicCounter.Services
{
    public class RecreateEventsQueueStorage : AppStorage
    {
        public override async Task CreateStorage()
        {
            var createQueue = Queues.GetQueueReference(RecreateEventsQueueName);
            await createQueue.CreateIfNotExistsAsync();
        }
    }
}
