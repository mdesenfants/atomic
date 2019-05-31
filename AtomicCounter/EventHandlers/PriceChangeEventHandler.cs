using AtomicCounter.Models.Events;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace AtomicCounter.EventHandlers
{
    public static class PriceChangeEventHandler
    {
        [FunctionName(nameof(PriceChangeEventHandler))]
        public static async Task Run(
            [QueueTrigger(AppStorage.PriceChangeEventsQueueName, Connection = "AzureWebJobsStorage")]PriceChangeEvent change,
            ILogger log)
        {
            log.LogInformation($"Handling: {change.Counter} change to {change.Amount} effective {change.Effective?.ToString("o", CultureInfo.InvariantCulture) ?? "immediately"}.");

            change.Effective = change.Effective ?? DateTimeOffset.UtcNow;

            await AppStorage.HandlePriceChangeEventAsync(change).ConfigureAwait(false);
            log.LogInformation($"Complete: {change}");
        }
    }
}