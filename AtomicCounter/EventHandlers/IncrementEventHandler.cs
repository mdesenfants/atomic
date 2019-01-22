using AtomicCounter.Models.Events;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
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
            log.LogInformation($"Handling: {increment}");
            var counter = new CountStorage(increment.Counter, log);
            try
            {
                await counter.IncrementAsync(increment.Count);
                log.LogInformation($"Complete: {increment}");
            }
            catch
            {
                var locks = counter.GetCounterLockQueue();
                if (await locks.ExistsAsync())
                {
                    await locks.CreateAsync();
                }

                var table = counter.GetCounterTable();
                if (!await table.ExistsAsync())
                {
                    await AppStorage.SendRecreateEventAsync(increment.Counter);
                }
            }
        }
    }
}