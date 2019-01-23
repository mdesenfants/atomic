using AtomicCounter.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Threading.Tasks;

namespace AtomicCounter.Test
{
    [TestClass]
    public class Initialize
    {
        public const string Counter = "test_counter";

        public static CloudStorageAccount Storage;

        [AssemblyInitialize]
        public static async Task AssemblyInitialize(TestContext context)
        {
            Environment.SetEnvironmentVariable("AzureWebJobsStorage", "UseDevelopmentStorage=true;");
            Storage = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));

            var queueClient = Storage.CreateCloudQueueClient();
            var increments = queueClient.GetQueueReference("increment-items");
            await increments.DeleteIfExistsAsync();

            var tableClient = Storage.CreateCloudTableClient();
            var table = tableClient.GetTableReference(CountStorage.Tableize(Counter));
            await table.DeleteIfExistsAsync();

            var blobClient = Storage.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(AppStorage.CountersKey);
            await blobClient.GetContainerReference(AppStorage.CountersKey).GetBlockBlobReference(Counter).DeleteIfExistsAsync();
        }
    }
}
