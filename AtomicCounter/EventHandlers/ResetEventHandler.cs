using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AtomicCounter.EventHandlers
{
    public static class ResetEventHandler
    {
        [FunctionName(nameof(ResetEventHandler))]
        public static async Task Run(
            [QueueTrigger(AppStorage.ResetEventsQueueName, Connection = "AzureWebJobsStorage")]string counter,
            ILogger log)
        {
            log.LogInformation($"Resetting: {counter}");
            await AppStorage.DeleteCounterAsync(counter, log).ConfigureAwait(false);
            await AppStorage.SendRecreateEventAsync(counter).ConfigureAwait(false);
        }
    }
}
