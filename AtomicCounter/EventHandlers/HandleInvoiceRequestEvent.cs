using AtomicCounter.Models;
using AtomicCounter.Models.Events;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AtomicCounter.EventHandlers
{
    public static class HandleInvoiceRequestEvent
    {
        [FunctionName("HandleInvoiceRequestEvent")]
        public static async Task Run(
            [QueueTrigger(AppStorage.InvoicRequestEventsQueueName, Connection = "AzureWebJobsStorage")]InvoiceRequestEvent request,
            ILogger log)
        {
            log.LogInformation($"Handling invoice request: {request.Counter}");

            var client = new CountStorage(request.Counter, log);
            var data = await client.GetInvoiceDataAsync(request.Min, request.Max).ConfigureAwait(false);

            foreach (var record in data)
            {
                await AppStorage.SaveInvoiceAsync(request.Counter, record.Key, request.Min, request.Max, record.Value).ConfigureAwait(false);
            }

            var meta = await AppStorage.GetCounterMetadataAsync(request.Counter).ConfigureAwait(false);

            meta.LastInvoiceRun = request.Max;
            meta.NextInvoiceRun = GetNextDate(request.Max, meta.InvoiceFrequency);

            await AppStorage.SaveCounterMetadataAsync(meta).ConfigureAwait(false);
        }

        public static DateTimeOffset GetNextDate(DateTimeOffset current, InvoiceFrequency invoiceFrequency)
        {
            switch (invoiceFrequency)
            {
                case InvoiceFrequency.Never:
                    return DateTimeOffset.MaxValue;
                case InvoiceFrequency.Weekly:
                    return current.AddDays(7);
                case InvoiceFrequency.EveryOtherWeek:
                    return current.AddDays(14);
                case InvoiceFrequency.TwiceMonthly:
                    var first = new DateTimeOffset(current.Year, current.Month, 1, 0, 0, 0, current.Offset);
                    var last = first.AddMonths(1).AddDays(-1);
                    var middleDay = (int)(((last.Day - first.Day) / 2.0) + 0.5);
                    var middle = new DateTimeOffset(last.Year, last.Month, middleDay, 0, 0, 0, last.Offset);
                    return current > middle ? first.AddMonths(1) : middle;
                case InvoiceFrequency.Monthly:
                    return current.AddMonths(1);
                case InvoiceFrequency.Quarterly:
                    return current.AddMonths(3);
                case InvoiceFrequency.TwiceAnnually:
                    return current.AddMonths(6);
                case InvoiceFrequency.Annually:
                    return current.AddYears(1);
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
