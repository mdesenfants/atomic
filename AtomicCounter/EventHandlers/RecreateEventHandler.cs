using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AtomicCounter.EventHandlers
{
    public static class RecreateEventHandler
    {
        [FunctionName(nameof(RecreateEventHandler))]
        public static async Task Run(
            [QueueTrigger(AppStorage.RecreateEventsQueueName, Connection = "AzureWebJobsStorage")]string counter,
            ILogger log)
        {
            log.LogInformation($"Recreating: {counter}");
            await AppStorage.RecreateCounterAsync(counter, log);
        }
    }
}
