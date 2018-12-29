using System.Threading.Tasks;
using AtomicCounter.Models.Events;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

namespace AtomicCounter.Services
{
    public static class AppStorage
    {
        public const string CountQueueName = "increment-items";
        public static async Task SendIncrementEventAsync(string tenant, string app, string counter, long count = 1)
        {
            var storageAccount = CloudStorageAccount.Parse(System.Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            var queueClient = storageAccount.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference(CountQueueName);
            await queue.CreateIfNotExistsAsync();

            var message = new CloudQueueMessage(new IncrementEvent()
            {
                App = app,
                Tenant = tenant,
                Count = count,
                Counter = counter
            }.ToString());

            await queue.AddMessageAsync(message);
        }
    }
}
