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
        public static async Task Run([TimerTrigger("0 5 */1 * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"Generating invoices starting at: {DateTime.Now}");

            var container = AppStorage.GetCounterMetadataContainer();

            BlobContinuationToken token = null;
            do
            {
                var segment = await container.ListBlobsSegmentedAsync(token);
                token = segment.ContinuationToken;

                foreach (var record in segment.Results.OfType<CloudBlockBlob>())
                {
                    // Check last & next invoice generation date
                    // If next is past, update dates and submit range to queue
                }
            } while (token != null);
        }
    }
}
