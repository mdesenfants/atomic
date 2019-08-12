using AtomicCounter.Models.Events;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AtomicCounter.EventHandlers
{
    public static class IncrementEventHandler
    {
        [FunctionName(nameof(IncrementEventHandler))]
        public static async Task Run(
            [QueueTrigger(AppStorage.CountQueueName, Connection = "AzureWebJobsStorage")]IncrementEvent increment,
            ILogger log)
        {
            if (increment == null)
            {
                throw new ArgumentNullException(nameof(increment));
            }

            log.LogInformation($"Handling: {increment}");
            var counter = new CountStorage(increment.Counter, log);
            try
            {
                await counter.IncrementAsync(increment.EventId, increment.Count, increment.Value);
                log.LogInformation($"Complete: {increment}");
            }
            catch
            {
                if (!await counter.GetCounterTable().ExistsAsync())
                {
                    await AppStorage.SendRecreateEventAsync(increment.Counter);
                }

                throw;
            }
        }
    }
}