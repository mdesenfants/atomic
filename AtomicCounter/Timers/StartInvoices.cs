using AtomicCounter.Models;
using AtomicCounter.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AtomicCounter.Timers
{
    public static class StartInvoices
    {
        [FunctionName(nameof(StartInvoices))]
        public static async Task Run([TimerTrigger("0 5 */1 * * *")]TimerInfo timer, ILogger log)
        {
            log.LogInformation($"Generating invoices starting at: {DateTime.Now}");
            log.LogInformation(timer?.ToString());

            var container = AppStorage.GetCounterMetadataContainer();

            BlobContinuationToken token = null;
            do
            {
                var segment = await container.ListBlobsSegmentedAsync(token).ConfigureAwait(false);
                token = segment.ContinuationToken;

                foreach (var record in segment.Results.OfType<CloudBlockBlob>())
                {
                    var counter = await AppStorage.GetCounterMetadataAsync(record.Name).ConfigureAwait(false);

                    if (counter.InvoiceFrequency == InvoiceFrequency.Never ||
                        counter.NextInvoiceRun > DateTimeOffset.Now)
                    {
                        return;
                    }

                    await AppStorage.SendInvoiceRequestEventAsync(counter.CounterName, counter.LastInvoiceRun, counter.NextInvoiceRun).ConfigureAwait(false);
                }
            } while (token != null);
        }
    }
}
