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
        private const string everyFiveMinutes = "* */5 * * * *";

        [FunctionName("Antidote")]
        public static async Task Run(
            [TimerTrigger(everyFiveMinutes)]TimerInfo timer,
            ILogger log,
            CancellationToken token)
        {
            if (timer == null)
            {
                throw new ArgumentNullException(nameof(timer));
            }

            log.LogInformation($"Retrying poison items at {DateTime.Now}. Timer is {(timer.IsPastDue ? "past due" : "on time")}.");

            AppStorage.CreateAppStorage();

            var total = await AppStorage.RetryPoisonIncrementEventsAsync(log, token).ConfigureAwait(false);
            
            log.LogInformation($"Resubmitted {total} increment operations from poison queue.");
        }
    }
}
