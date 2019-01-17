using System;
using System.Threading;
using System.Threading.Tasks;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace AtomicCounter.Timers
{
    public static class Antidote
    {
        private const string everyFiveMinutes = "*/5 * * * *";

        [FunctionName("Antidote")]
        public static async Task Run(
            [TimerTrigger(everyFiveMinutes)]TimerInfo myTimer,
            ILogger log,
            CancellationToken token)
        {
            log.LogInformation($"Retrying poison items at {DateTime.Now}.");

            var total = await AppStorage.RetryPoisonIncrementEventsAsync(token);
            
            log.LogInformation($"Resubmitted {total} increment operations from poison queue.");
        }
    }
}
