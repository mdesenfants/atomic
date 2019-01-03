using System.Threading.Tasks;
using AtomicCounter.Models.Events;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AtomicCounter.EventHandlers
{
    public static class IncrementEventHandler
    {
        [FunctionName("IncrementEventHandler")]
        public static async Task Run(
            [QueueTrigger(AppStorage.CountQueueName, Connection = "AzureWebJobsStorage")]IncrementEvent increment,
            ILogger log)
        {
            log.LogInformation($"Handling: {increment}");
            var counter = new CounterStorage(increment.Tenant, increment.App, increment.Counter);
            await counter.IncrementAsync(increment.Count);
            log.LogInformation($"Complete: {increment}");
        }
    }
}
