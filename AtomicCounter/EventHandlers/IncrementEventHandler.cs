using System.Threading.Tasks;
using AtomicCounter.Models.Events;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace AtomicCounter.EventHandlers
{
    public static class IncrementEventHandler
    {
        [FunctionName("IncrementEventHandler")]
        public static async Task Run(
            [QueueTrigger(AppStorage.CountQueueName, Connection = "AzureWebJobsStorage")]IncrementEvent increment,
            TraceWriter log)
        {
            log.Info($"Handling: {increment}");
            var counter = new CounterStorage(increment.Tenant, increment.App, increment.Counter);
            await counter.IncrementAsync();
            log.Info($"Complete: {increment}");
        }
    }
}
