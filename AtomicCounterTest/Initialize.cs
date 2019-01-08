using System;
using System.Threading.Tasks;
using AtomicCounter.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;

namespace AtomicCounter.Test
{
    [TestClass]
    public class Initialize
    {
        public const string Tenant = "test_tenant";
        public const string App = "test_app";
        public const string Counter = "test_count";

        public static CloudStorageAccount Storage;

        [AssemblyInitialize]
        public static async Task AssemblyInitialize(TestContext context)
        {
            Environment.SetEnvironmentVariable("AzureWebJobsStorage", "UseDevelopmentStorage=true;");
            Storage = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));

            var queueClient = Storage.CreateCloudQueueClient();
            var queue = queueClient.GetQueueReference($"{CounterStorage.Sanitize(Tenant)}-{CounterStorage.Sanitize(App)}-{CounterStorage.Sanitize(Counter)}");
            await queue.DeleteIfExistsAsync();
            var increments = queueClient.GetQueueReference("increment-items");
            await queue.DeleteIfExistsAsync();

            var tableClient = Storage.CreateCloudTableClient();
            var table = tableClient.GetTableReference(CounterStorage.Tableize(Tenant+"counts"));
            await table.DeleteIfExistsAsync();

            var blobClient = Storage.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference("tenants");
            await blobClient.GetContainerReference("tenants").GetBlockBlobReference(Tenant).DeleteIfExistsAsync();
        }
    }
}
